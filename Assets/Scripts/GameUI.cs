using Cysharp.Threading.Tasks;
using DG.Tweening.Core.Easing;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject panelStart;
    [SerializeField] private GameObject panelResult;
    [SerializeField] private TMP_Text timeTxt;
    [SerializeField] private TMP_Text resultTxt;
    [SerializeField] private TMP_Text levelTxt;
    [SerializeField] private TMP_Dropdown gameModeDropdown;
    [SerializeField] private TMP_Dropdown levelDropdown;

    private void Start()
    {
        GameManager.Instance.OnTimeChanged += OnTimeChanged;
        Init();
    }

    private void OnTimeChanged()
    {
        int minutes = (int)(GameManager.Instance.RemainTime / 60);
        int seconds = (int)(GameManager.Instance.RemainTime % 60);
        timeTxt.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void Init()
    {
        gameModeDropdown.ClearOptions();
        List<string> modeOptions = new() { "1 VS 1", "1 VS Many", "Many VS Many" };
        gameModeDropdown.AddOptions(modeOptions);
        gameModeDropdown.value = 0; 
        gameModeDropdown.RefreshShownValue();

        levelDropdown.ClearOptions();
        List<string> levelOptions = new();
        for (int i = 1; i <= 10; i++)
        {
            levelOptions.Add($"Level {i}");
        }
        levelDropdown.AddOptions(levelOptions);
        levelDropdown.value = 0; 
        levelDropdown.RefreshShownValue();

        panelResult.SetActive(false);
    }
    public async UniTask ShowPanelResult(TeamType teamType)
    {
        AudioManager.Instance.StopMusic();

        await UniTask.Delay(2000);
        string result = "Victory";
        if(teamType == TeamType.TeamB)
        {
            result = "Defeated!";
        }
        resultTxt.text = result;
        panelResult.SetActive(true);

        if (teamType == TeamType.TeamA)
        {
            AudioManager.Instance.PlaySoundEffect(AudioName.Win);
        }
        else
        {
            AudioManager.Instance.PlaySoundEffect(AudioName.Lose);
        }
    }
    public void OnContinueButtonClick()
    {
        AudioManager.Instance.PlayMusic(AudioName.HomeBG);

        panelResult.SetActive(false);
        panelStart.SetActive(true);
    }

    public void OnPlayButtonClicked()
    {
        GameMode mode = (GameMode)gameModeDropdown.value;
        int level = levelDropdown.value;

        levelTxt.text = $"Level {level + 1}";
        GameManager.Instance.SetupGame(mode, level);

        panelStart.SetActive(false); 
    }
    private void OnDestroy()
    {
        GameManager.Instance.OnTimeChanged -= OnTimeChanged;
    }
}