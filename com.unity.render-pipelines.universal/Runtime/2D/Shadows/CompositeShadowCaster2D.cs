using UnityEngine.Scripting.APIUpdating;


namespace UnityEngine.Rendering.Universal
{
    [AddComponentMenu("Rendering/2D/Composite Shadow Caster 2D (Experimental)")]
    [ExecuteInEditMode]
    [MovedFrom("UnityEngine.Experimental.Rendering.Universal")]
    public class CompositeShadowCaster2D : ShadowCasterGroup2D
    {
        protected void OnEnable()
        {
            ShadowCasterGroup2DManager.AddGroup(this);
        }

        protected void OnDisable()
        {
            ShadowCasterGroup2DManager.RemoveGroup(this);
        }
    }
}
