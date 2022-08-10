using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    class AdaptiveVTBufferManager
    {
        public static TextureHandle CreateAdaptiveFeedbackBuffer(RenderGraph renderGraph)
        {
#if UNITY_2020_2_OR_NEWER
            FastMemoryDesc colorFastMemDesc;
            colorFastMemDesc.inFastMemory = true;
            colorFastMemDesc.residencyFraction = 1.0f;
            colorFastMemDesc.flags = FastMemoryFlags.SpillTop;
#endif

            return renderGraph.CreateTexture(
                new TextureDesc(Vector2.one)
                {
                    colorFormat = GetFeedbackBufferFormat(),
                    enableRandomWrite = true,
                    bindTextureMS = false,
                    msaaSamples = MSAASamples.None,
                    clearBuffer = true,
                    clearColor = Color.white,
                    name = "AdaptiveVTFeedback",
                    fallBackToBlackTexture = true
#if UNITY_2020_2_OR_NEWER
                    ,
                    fastMemoryDesc = colorFastMemDesc
#endif
                }); ;;
        }

        public static GraphicsFormat GetFeedbackBufferFormat()
        {
            return GraphicsFormat.R8G8B8A8_UNorm;
        }
    }
}
