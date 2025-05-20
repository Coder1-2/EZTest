using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameMode
{
    OneVsOne,
    OneVsMany,
    ManyVsMany
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private CharacterData playerPrefab; // Prefab cho player (CharacterData)
    [SerializeField] private CharacterData aiTeamAPrefab; // Prefab cho AI (AIData)
    [SerializeField] private CharacterData aiTeamBPrefab; // Prefab cho AI (AIData)
    [SerializeField] private GameMode currentMode = GameMode.OneVsOne;
    [SerializeField] private int currentLevel = 1;

    [Header("Pool Settings")]
    [SerializeField] private int initialAITeamAPoolSize = 24; 
    [SerializeField] private int initialAITeamBPoolSize = 25; 

    private List<CharacterData> _teamA = new ();
    private List<CharacterData> _teamB = new ();
    private CharacterData _playerInstance;
    private Queue<CharacterData> aiTeamAPool = new ();
    private Queue<CharacterData> aiTeamBPool = new ();

    public List<CharacterData> TeamA => _teamA;
    public List<CharacterData> TeamB => _teamB;
    private bool _isGameRunning;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Khởi tạo player và pool
        InitializePlayer();
        InitializeAIPools();

        // Khởi tạo game với mode và level mặc định
        SetupGame(currentMode, currentLevel);
    }

    private void InitializePlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned!");
            return;
        }

        // Tạo instance duy nhất cho player
        _playerInstance = Instantiate(playerPrefab);
        _playerInstance.gameObject.SetActive(false); 
    }

    private void InitializeAIPools()
    {
        if (aiTeamAPrefab == null || aiTeamBPrefab == null)
        {
            Debug.LogError("AI prefab is not assigned!");
            return;
        }

        // Khởi tạo pool cho AI TeamA
        for (int i = 0; i < initialAITeamAPoolSize; i++)
        {
            var obj = Instantiate(aiTeamAPrefab);
            obj.gameObject.SetActive(false);
            aiTeamAPool.Enqueue(obj);
        }

        // Khởi tạo pool cho AI TeamB
        for (int i = 0; i < initialAITeamBPoolSize; i++)
        {
            var obj = Instantiate(aiTeamBPrefab);
            obj.gameObject.SetActive(false);
            aiTeamBPool.Enqueue(obj);
        }
    }

    public void SetupGame(GameMode mode, int level)
    {
        _isGameRunning = false;
        // Trả đội cũ về pool
        ClearTeams();

        // Sinh đội dựa trên mode
        switch (mode)
        {
            case GameMode.OneVsOne:
                SetupOneVsOne(level);
                break;
            case GameMode.OneVsMany:
                SetupOneVsMany(level);
                break;
            case GameMode.ManyVsMany:
                SetupManyVsMany(level);
                break;
        }

        _isGameRunning = true;

        Debug.Log($"Game setup: Mode={mode}, Level={level}, TeamA={_teamA.Count}, TeamB={_teamB.Count}");
    }
    private void Update()
    {
        if (!_isGameRunning) return;

        var teamACount = _teamA.Count;
        for(var i = 0; i < teamACount; i++)
        {
            var aiTeamA = _teamA[i];
            aiTeamA.UpdateCharacter();
        }

        var teamBCount = _teamB.Count;
        for (var i = 0; i < teamBCount; i++)
        {
            var aiTeamB = _teamB[i];
            aiTeamB.UpdateCharacter();
        }

        if(_playerInstance != null)
        {
            _playerInstance.UpdateCharacter();
        }
    }

    private void ClearTeams()
    {
        // Trả TeamA về pool
        foreach (var character in _teamA)
        {
            if (character != null)
            {
                character.gameObject.SetActive(false);
                if (character is AIData)
                    aiTeamAPool.Enqueue(character);
                // Player không trả về pool, chỉ tắt
            }
        }

        // Trả TeamB về pool
        foreach (var character in _teamB)
        {
            if (character != null)
            {
                character.gameObject.SetActive(false);
                aiTeamBPool.Enqueue(character);
            }
        }

        _teamA.Clear();
        _teamB.Clear();
    }

    private void SetupOneVsOne(int level)
    {
        // TeamA: 1 player
        CharacterData player = SpawnPlayer(level, new Vector3(-5, 0, 0));
        _teamA.Add(player);

        // TeamB: 1 AI
        CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, new Vector3(5, 0, 0));
        _teamB.Add(ai);
    }

    private void SetupOneVsMany(int level)
    {
        // TeamA: 1 player
        CharacterData player = SpawnPlayer(level, new Vector3(-5, 0, 0));
        _teamA.Add(player);

        // TeamB: 2-5 AI
        int aiCount = 2 + Mathf.FloorToInt((level - 1) / 3); // 2 AI ở level 1-3, 3 ở 4-6, 4 ở 7-9, 5 ở 10
        for (int i = 0; i < aiCount; i++)
        {
            Vector3 position = new Vector3(5 + i * 2, 0, 0);
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, position);
            _teamB.Add(ai);
        }
    }

    private void SetupManyVsMany(int level)
    {
        // TeamA: 1 player + 1-3 AI
        CharacterData player = SpawnPlayer(level, new Vector3(-5, 0, 0));
        _teamA.Add(player);

        int aiCountTeamA = 1 + Mathf.FloorToInt((level - 1) / 4); // 1 AI ở level 1-4, 2 ở 5-8, 3 ở 9-10
        for (int i = 0; i < aiCountTeamA; i++)
        {
            Vector3 position = new Vector3(-5 + i * 2, 0, 2);
            CharacterData ai = SpawnAI(aiTeamAPool, TeamType.TeamA, level, position);
            _teamA.Add(ai);
        }

        // TeamB: 2-4 AI
        int aiCountTeamB = aiCountTeamA + 1;
        for (int i = 0; i < aiCountTeamB; i++)
        {
            Vector3 position = new Vector3(5 + i * 2, 0, 0);
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, position);
            _teamB.Add(ai);
        }
    }

    private CharacterData SpawnPlayer(int level, Vector3 position)
    {
        if (_playerInstance == null)
        {
            Debug.LogError("Player instance is not initialized!");
            return null;
        }

        _playerInstance.gameObject.SetActive(true);
        _playerInstance.transform.position = position;
        _playerInstance.transform.rotation = Quaternion.identity;

        _playerInstance.Init(level, TeamType.TeamA);

        return _playerInstance;
    }

    private CharacterData SpawnAI(Queue<CharacterData> pool, TeamType team, int level, Vector3 position)
    {
        CharacterData ai;

        // Lấy từ pool hoặc tạo mới
        if (pool.Count > 0)
        {
            ai = pool.Dequeue();
            ai.gameObject.SetActive(true);
        }
        else
        {
            var prefab = team == TeamType.TeamA ? aiTeamAPrefab : aiTeamBPrefab;
            ai = Instantiate(prefab);
            Debug.LogWarning($"Pool for AI {team} is empty, instantiating new object.");
        }

        ai.transform.SetPositionAndRotation(position, Quaternion.identity);

        ai.Init(level, team);

        return ai;
    }

    public void ChangeGameMode(GameMode mode, int level)
    {
        currentMode = mode;
        currentLevel = level;
        SetupGame(mode, level);
    }
}