using UnityEngine;

// Lässt ein Bauteil pulsierend einfärben, solange es im aktuellen Wartungsschritt dran ist.
// MaterialPropertyBlock statt renderer.material, damit keine Materialkopien entstehen.
public class PartHighlighter : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] string partId;
    [SerializeField] Renderer[] renderers;
    [SerializeField] Color highlightColor = new Color(1f, 0.55f, 0f);
    [SerializeField] float pulseSpeed = 4f;

    Color[] originalColors;
    MaterialPropertyBlock block;
    bool highlighted;

    public string PartId => partId;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        block = new MaterialPropertyBlock();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            originalColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
    }

    public void SetHighlighted(bool value)
    {
        highlighted = value;
        if (!highlighted)
            ApplyColor(1f);
    }

    void Update()
    {
        if (!highlighted)
            return;

        // 0..1 Sinus für das Pulsieren
        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        ApplyColor(1f - t);
    }

    void ApplyColor(float originalWeight)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(highlightColor, originalColors[i], originalWeight));
            renderers[i].SetPropertyBlock(block);
        }
    }
}
