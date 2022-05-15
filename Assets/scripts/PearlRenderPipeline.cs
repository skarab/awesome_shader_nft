namespace Pearl
{
	using UnityEngine;
	using UnityEngine.Rendering;
#if UNITY_EDITOR
	using UnityEditor;
#endif
	using System;


	public class PearlRenderPipeline : RenderPipeline
	{
		public static class ShaderTags
		{
			public static readonly ShaderTagId MainPass = new ShaderTagId("ForwardBase");
			public static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit");
		}

		public Action<ScriptableRenderContext, CommandBuffer, Camera, CullingResults> OnRenderCamera;

		private PearlRenderPipelineAsset _renderPipelineAsset = null;
		private CommandBuffer _commandBuffer = null;

		public PearlRenderPipelineAsset Asset { get { return _renderPipelineAsset; } }

		public PearlRenderPipeline(PearlRenderPipelineAsset renderPipelineAsset)
		{
			_renderPipelineAsset = renderPipelineAsset;

			_commandBuffer = new CommandBuffer()
			{
				name = GetType().Name
			};
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (_commandBuffer != null)
			{
				_commandBuffer.Release();
				_commandBuffer = null;
			}
		}

		protected override void Render(ScriptableRenderContext context, Camera[] cameras)
		{
			BeginFrameRendering(context, cameras);

			int camerasCount = cameras.Length;
			for (int i = 0; i < camerasCount; ++i)
			{
				Camera camera = cameras[i];
				BeginCameraRendering(context, camera);

				CullingResults cullingResults;
				ScriptableCullingParameters cullingParams;
				if (camera.TryGetCullingParameters(out cullingParams) == false)
				{
					cullingResults = new CullingResults();
					continue;
				}
				cullingResults = context.Cull(ref cullingParams);

				context.SetupCameraProperties(camera);

#if UNITY_EDITOR
				if (camera.cameraType == CameraType.SceneView && Handles.ShouldRenderGizmos())
				{
					ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
				}
#endif

				//_commandBuffer.SetRenderTarget(camera.targetTexture);
				//_commandBuffer.SetViewport(new Rect(0, 0, camera.pixelWidth, camera.pixelHeight));
				//_commandBuffer.ClearRenderTarget(true, true, camera.backgroundColor);
				//context.ExecuteCommandBuffer(_commandBuffer);
				//_commandBuffer.Clear();

				if (OnRenderCamera != null)
				{
					OnRenderCamera(context, _commandBuffer, camera, cullingResults);
				}

				/*
				SortingSettings sortingSettings = new SortingSettings(camera);
				DrawingSettings drawSettings = new DrawingSettings(ShaderTags.MainPass, sortingSettings)
				{
					perObjectData = 0
				};
				FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all);

				sortingSettings.criteria = SortingCriteria.CommonOpaque;
				drawSettings.sortingSettings = sortingSettings;
				filterSettings.renderQueueRange = RenderQueueRange.opaque;
				context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);

#if UNITY_EDITOR
				DrawDefaultObjects(ref context, camera, cullingResults);

				if (Handles.ShouldRenderGizmos())
				{
					context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
				}
#endif
				*/

				context.Submit();
				EndCameraRendering(context, camera);
			}

			EndFrameRendering(context, cameras);
		}

		private void DrawDefaultObjects(ref ScriptableRenderContext context, Camera camera, CullingResults cullingResults, int layerMask = -1)
		{
			// Setup DrawSettings and FilterSettings
			SortingSettings sortingSettings = new SortingSettings(camera)
			{
				criteria = SortingCriteria.CommonTransparent
			};

			ShaderTagId defaultPass = ShaderTags.SRPDefaultUnlit;
			DrawingSettings drawSettings = new DrawingSettings(defaultPass, sortingSettings)
			{
				perObjectData = PerObjectData.None
			};
			//This will let you draw shader passes without the LightMode,
			//thus it draws the default UGUI materials
			drawSettings.SetShaderPassName(1, defaultPass);

			FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all, layerMask);

			context.DrawRenderers(cullingResults, ref drawSettings, ref filterSettings);
		}
	}
}
