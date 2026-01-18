using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    Resolution[] resolutions;
    public TMP_Dropdown resolutionDropdown;

    void Start()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();

        int currentResIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height + " " + resolutions[i].refreshRateRatio + "hz";
            options.Add(option);

            if(resolutions[i].Equals(Screen.currentResolution))
            {
                currentResIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution (int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }
    public void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
    }
}
