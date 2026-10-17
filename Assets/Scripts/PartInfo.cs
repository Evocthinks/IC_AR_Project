using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Antippen eines Bauteils zeigt dessen Infotext im Infopanel.
// Braucht einen Collider am Bauteil und einen Physics Raycaster an der AR Camera.
[RequireComponent(typeof(Collider))]
public class PartInfo : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string displayName;
    [SerializeField, TextArea(3, 6)] string description;
    [SerializeField] GameObject infoPanel;
    [SerializeField] TMP_Text infoTitle;
    [SerializeField] TMP_Text infoBody;

    public void OnPointerClick(PointerEventData eventData)
    {
        infoTitle.text = displayName;
        infoBody.text = description;
        infoPanel.SetActive(true);
    }
}
