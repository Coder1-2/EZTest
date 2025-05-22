using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "GameSetting", menuName = "Game/GameSetting")]
public class GameSettings : ScriptableObject
{
    [Header("Player prefab")]
    public CharacterData player;

    [Header("AI Team A prefab")]
    public AIData AITeamA;

    [Header("AI Team B prefab")]
    public AIData AITeamB;

    [Header("Effect Hit prefab")]
    public GameObject effectHit;

    [Header("Hit Text Data")]
    public DynamicTextData textData;

    [Header("Level Config")]
    public LevelDataConfig levelConfig;

    public static explicit operator GameSettings(ResourceRequest v)
    {
        throw new NotImplementedException();
    }
}
