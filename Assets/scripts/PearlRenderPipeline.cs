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

				if (OnRenderCamera != null)
				{
					OnRenderCamera(context, _commandBuffer, camera, cullingResults);
				}

				context.Submit();
				EndCameraRendering(context, camera);
			}

			EndFrameRendering(context, cameras);
		}
	}
}
