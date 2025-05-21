using System;
using System.Collections.Generic;
using Cinemachine;
using Cysharp.Threading.Tasks;
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
    [SerializeField] private Joystick joystick;
    [SerializeField] private CharacterData playerPrefab; // Prefab cho player (CharacterData)
    [SerializeField] private CharacterData aiTeamAPrefab; // Prefab cho AI (AIData)
    [SerializeField] private CharacterData aiTeamBPrefab; // Prefab cho AI (AIData)
    [SerializeField] private GameMode currentMode = GameMode.OneVsOne;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [Header("Spawn Areas")]
    [SerializeField] private Transform spawnAreaTeamA;
    [SerializeField] private Transform spawnAreaTeamB;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(4, 0, 4);

    private readonly int _initialAITeamAPoolSize = 24; 
    private readonly int _initialAITeamBPoolSize = 25; 

    private List<CharacterData> _teamA = new ();
    private List<CharacterData> _teamB = new ();
    private CharacterData _playerInstance;
    private Queue<CharacterData> aiTeamAPool = new ();
    private Queue<CharacterData> aiTeamBPool = new ();

    public List<CharacterData> TeamA => _teamA;
    public List<CharacterData> TeamB => _teamB;
    private bool _isGameRunning;

    public Action<TeamType> OnGameOver;

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
        for (int i = 0; i < _initialAITeamAPoolSize; i++)
        {
            var obj = Instantiate(aiTeamAPrefab);
            obj.gameObject.SetActive(false);
            aiTeamAPool.Enqueue(obj);
        }

        // Khởi tạo pool cho AI TeamB
        for (int i = 0; i < _initialAITeamBPoolSize; i++)
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
    private void LateUpdate()
    {
        if (!_isGameRunning) return;

        var teamACount = _teamA.Count;
        for (var i = 0; i < teamACount; i++)
        {
            var aiTeamA = _teamA[i];
            aiTeamA.UpdateHealthBarView();
        }

        var teamBCount = _teamB.Count;
        for (var i = 0; i < teamBCount; i++)
        {
            var aiTeamB = _teamB[i];
            aiTeamB.UpdateHealthBarView();
        }

        if (_playerInstance != null)
        {
            _playerInstance.UpdateHealthBarView();
        }
    }
    public void ReturnToPool(CharacterData character, TeamType team)
    {
        character.gameObject.SetActive(false);
        if (team == TeamType.TeamA)
        {
            aiTeamAPool.Enqueue(character);
            _teamA.Remove(character);
        }
        else 
        {
            aiTeamBPool.Enqueue(character);
            _teamB.Remove(character);
        } 
    }
    public async UniTask DelayReturnToPool(CharacterData character, TeamType team, float delay = 1)
    {
        if (team == TeamType.TeamA)
        {
            if (_teamA.Count <= 1)
            {
                _isGameRunning = false;
                OnGameOver?.Invoke(TeamType.TeamB);
            }
        }
        else
        {
            if (_teamB.Count <= 1)
            {
                _isGameRunning = false;
                OnGameOver?.Invoke(TeamType.TeamA);
            }
        }
        await UniTask.Delay((int)(delay * 3000));
        ReturnToPool(character, team);
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
    private List<Vector3> GetRandomSpawnPositions(Vector3 center, int count, Vector3 areaSize)
    {
        List<Vector3> positions = new();
        float minDistance = 0.5f; // Khoảng cách tối thiểu giữa các vị trí

        if (areaSize.x <= 0 || areaSize.z <= 0)
        {
            Debug.LogWarning($"Invalid spawnAreaSize: {areaSize}. Using default size (4, 0, 4)");
            areaSize = new Vector3(4, 0, 4);
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 position;
            int attempts = 0;
            const int maxAttempts = 30;

            do
            {
                float x = center.x + UnityEngine.Random.Range(-areaSize.x / 2, areaSize.x / 2);
                float z = center.z + UnityEngine.Random.Range(-areaSize.z / 2, areaSize.z / 2);
                position = new Vector3(x, center.y, z);

                bool tooClose = false;
                foreach (var pos in positions)
                {
                    if (Vector3.Distance(position, pos) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    positions.Add(position);
                    Debug.Log($"Spawn position {i + 1}/{count}: {position}");
                    break;
                }

                attempts++;
                if (attempts >= maxAttempts)
                {
                    Debug.LogWarning($"Could not find non-overlapping position after {maxAttempts} attempts. Using last position: {position}");
                    positions.Add(position);
                    break;
                }
            } while (true);
        }

        return positions;
    }

    private void SetupOneVsOne(int level)
    {
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, spawnAreaTeamB.position);
        _teamB.Add(ai);
        Debug.Log($"Setup OneVsOne: Player at {spawnAreaTeamA.position}, AI at {spawnAreaTeamB.position}");
    }

    private void SetupOneVsMany(int level)
    {
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        int aiCount = 2 + Mathf.FloorToInt((level - 1) / 3);
        var teamBPositions = GetRandomSpawnPositions(spawnAreaTeamB.position, aiCount, spawnAreaSize);
        for (int i = 0; i < aiCount; i++)
        {
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i]);
            _teamB.Add(ai);
        }
        Debug.Log($"Setup OneVsMany: Player at {spawnAreaTeamA.position}, {aiCount} AIs in TeamB");
    }

    private void SetupManyVsMany(int level)
    {
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        int aiCountTeamA = 1 + Mathf.FloorToInt((level - 1) / 4);
        var teamAPositions = GetRandomSpawnPositions(spawnAreaTeamA.position, aiCountTeamA, spawnAreaSize);
        for (int i = 0; i < aiCountTeamA; i++)
        {
            CharacterData ai = SpawnAI(aiTeamAPool, TeamType.TeamA, level, teamAPositions[i]);
            _teamA.Add(ai);
        }

        int aiCountTeamB = aiCountTeamA + 1;
        var teamBPositions = GetRandomSpawnPositions(spawnAreaTeamB.position, aiCountTeamB, spawnAreaSize);
        for (int i = 0; i < aiCountTeamB; i++)
        {
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i]);
            _teamB.Add(ai);
        }
        Debug.Log($"Setup ManyVsMany: Player + {aiCountTeamA} AIs in TeamA, {aiCountTeamB} AIs in TeamB");
    }
    private CharacterData SpawnPlayer(Vector3 position)
    {
        if (_playerInstance == null)
        {
            Debug.LogError("Player instance is not initialized!");
            return null;
        }

        _playerInstance.gameObject.SetActive(true);
        _playerInstance.transform.position = position;
        _playerInstance.transform.rotation = Quaternion.identity;

        _playerInstance.Init(0, TeamType.TeamA);
        _playerInstance.SetJoystick(joystick);

        virtualCamera.LookAt = _playerInstance.transform;
        virtualCamera.Follow = _playerInstance.transform;

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