namespace Pearl
{
	using System.Collections;
	using System.IO;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.Experimental.Rendering;
	using UnityEngine.Rendering;

	public class Generator : MonoBehaviour
	{
		private static readonly int _MaximumObjectCount = 80;
		private static readonly int _MaximumValuePerObjectCount = 10;
		private static readonly int _ObjectSize = sizeof(float) * 12;

		public static class Uniforms
		{
			// Generator
			public static readonly int Width = Shader.PropertyToID("_Width");
			public static readonly int Height = Shader.PropertyToID("_Height");
			public static readonly int Objects = Shader.PropertyToID("_Objects");
			public static readonly int ObjectValues = Shader.PropertyToID("_ObjectValues");
			public static readonly int Palette = Shader.PropertyToID("_Palette");

			// Render Textures
			public static readonly int OutputTex = Shader.PropertyToID("_OutputTex");

			// Raytracing
			public static readonly int AccelerationStructure = Shader.PropertyToID("_AccelerationStructure");
			public static readonly int Signature = Shader.PropertyToID("_Signature");
		}

		public static class Keywords
		{
			public static readonly string GenerateObjects = "GenerateObjects";
			public static readonly string RayTracingDispatch = "Dispatch";
			public static readonly string RayTracingPass = "RaytracingPass";
		}

		public ComputeShader generateObjectsShader = null;
		public RayTracingShader raytracingShader = null;
		public Shader raytraceObjectShader = null;
		public Color[] palette = null;
		public Texture2D signature = null;

		private int _kernelGenerateObjects = -1;
		private GraphicsBuffer _aabbsBuffer = null;
		private ComputeBuffer _objectsBuffer = null;
		private Material _raytraceObjectMaterial = null;
		private RayTracingAccelerationStructure.RASSettings _raytracingSettings;
		private RayTracingAccelerationStructure _raytracingAcceleration = null;
		private float[] _objectValues = null;
		private bool _hasRendered = false;

		private void OnEnable()
		{
			_kernelGenerateObjects = generateObjectsShader.FindKernel(Keywords.GenerateObjects);
			_objectsBuffer = new ComputeBuffer(_MaximumObjectCount, _ObjectSize, ComputeBufferType.Structured);
			generateObjectsShader.SetBuffer(_kernelGenerateObjects, Uniforms.Objects, _objectsBuffer);

			_aabbsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _MaximumObjectCount, sizeof(float) * 6);
			float[] objectsAABB = new float[6 * _MaximumObjectCount];
			for (int i = 0; i < _MaximumObjectCount; ++i)
			{
				objectsAABB[i * 6 + 0] = -10000.0f;
				objectsAABB[i * 6 + 1] = -10000.0f;
				objectsAABB[i * 6 + 2] = i + 1.0f;
				objectsAABB[i * 6 + 3] = 10000.0f;
				objectsAABB[i * 6 + 4] = 10000.0f;
				objectsAABB[i * 6 + 5] = i + 1.5f;
			}
			_aabbsBuffer.SetData(objectsAABB);

			_raytraceObjectMaterial = new Material(raytraceObjectShader);

			_raytracingSettings = new RayTracingAccelerationStructure.RASSettings();
			_raytracingSettings.layerMask = -1;
			_raytracingSettings.managementMode = RayTracingAccelerationStructure.ManagementMode.Manual;
			_raytracingSettings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
			_raytracingAcceleration = new RayTracingAccelerationStructure(_raytracingSettings);
			_raytracingAcceleration.AddInstance(_aabbsBuffer, (uint)_MaximumObjectCount, _raytraceObjectMaterial, false, false);
			_raytracingAcceleration.Build();
		}

		private void OnDisable()
		{
			if (_raytracingAcceleration != null)
			{
				_raytracingAcceleration.Release();
				_raytracingAcceleration = null;
			}

			_raytraceObjectMaterial = null;

			if (_objectsBuffer != null)
			{
				_objectsBuffer.Release();
				_objectsBuffer = null;
			}
		}

		private void OnRender(ScriptableRenderContext context, CommandBuffer commandBuffer, Camera camera, CullingResults cullingResults)
		{
			PearlRenderPipeline pipeline = RenderPipelineManager.currentPipeline as PearlRenderPipeline;
			if (pipeline == null)
			{
				return;
			}

			if (camera.pixelWidth != 180 || camera.pixelHeight != 180)
			{
				return;
			}

			if (_objectValues == null)
			{
				return;
			}

			commandBuffer.BeginSample("Create objects");
			commandBuffer.SetGlobalFloatArray(Uniforms.ObjectValues, _objectValues);
			commandBuffer.SetGlobalInt(Uniforms.Width, camera.pixelWidth);
			commandBuffer.SetGlobalInt(Uniforms.Height, camera.pixelHeight);

			Vector4[] paletteVectors = new Vector4[palette.Length];
			for (int i = 0; i < palette.Length; ++i)
			{
				paletteVectors[i] = palette[i];
			}

			commandBuffer.SetComputeVectorArrayParam(generateObjectsShader, Uniforms.Palette, paletteVectors);
			commandBuffer.DispatchCompute(generateObjectsShader, _kernelGenerateObjects, 1, 1, 1);
			commandBuffer.EndSample("Create objects");

			context.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Clear();

			RenderTargetIdentifier outputId = new RenderTargetIdentifier(Uniforms.OutputTex);

			commandBuffer.BeginSample("Raytracing");
			commandBuffer.GetTemporaryRT(Uniforms.OutputTex, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, GraphicsFormat.R8G8B8A8_UNorm, 1, true);
			commandBuffer.SetGlobalBuffer(Uniforms.Objects, _objectsBuffer);
			commandBuffer.SetGlobalTexture(Uniforms.Signature, signature);
			commandBuffer.SetRayTracingTextureParam(raytracingShader, Uniforms.OutputTex, outputId);
			commandBuffer.SetRayTracingShaderPass(raytracingShader, Keywords.RayTracingPass);
			commandBuffer.SetRayTracingAccelerationStructure(raytracingShader, Uniforms.AccelerationStructure, _raytracingAcceleration);
			commandBuffer.DispatchRays(raytracingShader, Keywords.RayTracingDispatch, (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
			commandBuffer.EndSample("Raytracing");

			context.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Clear();

			// Blit
			commandBuffer.SetRenderTarget(camera.targetTexture);
			commandBuffer.Blit(outputId, BuiltinRenderTextureType.CameraTarget);

			// Destroy Render Targets
			commandBuffer.ReleaseTemporaryRT(Uniforms.OutputTex);

			context.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Clear();

			_hasRendered = true;
		}


		private int _collectionId = 0;

		private void Start()
		{
			_collectionId = 0;
		}

		void Update()
		{
			if (RenderPipelineManager.currentPipeline != null)
			{
				PearlRenderPipeline pipeline = RenderPipelineManager.currentPipeline as PearlRenderPipeline;
				if (pipeline.OnRenderCamera == null)
				{
					pipeline.OnRenderCamera = OnRender;
				}
			}

			if (_collectionId == 1000)
			{
				EditorApplication.isPlaying = false;
				return;
			}

			Random.InitState(_collectionId);

			float[] objectValues = new float[_MaximumValuePerObjectCount * _MaximumObjectCount];
			for (int i = 0; i < _MaximumObjectCount; ++i)
			{
				for (int x = 0; x < _MaximumValuePerObjectCount; ++x)
				{
					objectValues[i * _MaximumValuePerObjectCount + x] = Random.value;
				}
			}
			_objectValues = objectValues;
		}

		private void LateUpdate()
		{
			if (_hasRendered)
			{
				StartCoroutine(RecordFrame());
			}
		}

		IEnumerator RecordFrame()
		{
			yield return new WaitForEndOfFrame();
			_objectValues = null;
			Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
			byte[] nftData = texture.EncodeToPNG();
			string nftPath = "../collection/awesome_shader_" + (_collectionId + 1).ToString("0000") + ".png";
			File.WriteAllBytes(nftPath, nftData);
			++_collectionId;
		}
	}
}
