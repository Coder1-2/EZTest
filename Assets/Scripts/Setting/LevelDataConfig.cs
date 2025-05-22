using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelData
{
    public float timeLimit;
}
[Serializable]
public class OneVsOneLevelData : LevelData
{
    public AILevel aiLevel;
}
[Serializable]
public class TeamAIData
{
    public TeamType aiTeam;
    public int aiQuantity;
    public float easyRate;
    public float mediumRate;
    public float hardRate;
}

[Serializable]
public class OneVsManyLevelData : LevelData
{
    public TeamAIData aiTeamB;
}
[Serializable]
public class ManyVsManyLevelData : LevelData
{
    public TeamAIData aiTeamA;
    public TeamAIData aiTeamB;
}


[CreateAssetMenu(fileName = "LevelDataConfig", menuName = "Game/LevelDataConfig", order = 1)]
public class LevelDataConfig : ScriptableObject
{
    public List<OneVsOneLevelData> oneVsOneLevels;
    public List<OneVsManyLevelData> oneVsManyLevels;
    public List<ManyVsManyLevelData> manyVsManyLevels;
}