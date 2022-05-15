namespace Pearl
{
	using UnityEngine;
	using UnityEngine.Rendering;

	[CreateAssetMenu(fileName = "PearlRenderPipelineAsset", menuName = "Pearl/PearlRenderPipelineAsset", order = 0)]
	public class PearlRenderPipelineAsset : RenderPipelineAsset
	{
		protected override RenderPipeline CreatePipeline()
		{
			return new PearlRenderPipeline(this);
		}
	}
}