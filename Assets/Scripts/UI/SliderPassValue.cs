using UnityEngine;
using UnityEngine.UI;

public class SliderPassValue : MonoBehaviour
{
    private Slider slider;
    void Awake()
    {
        slider = GetComponent<Slider>();
        Disable();
    }

    public void ChangeValue(float value)
    {
        slider.gameObject.SetActive(true);
        slider.value = value;
    }

    public void Disable()
    {
        slider.value = 0;
        slider.gameObject.SetActive(false);
    }
}
