using System.Collections;
using System.Collections.Generic;
using Cinemachine;
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
    public IEnumerator DelayReturnToPool(CharacterData character, TeamType team, float delay = 1)
    {
        yield return new WaitForSeconds(delay);
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
    private List<Vector3> GetVFormationPositions(Vector3 center, int count, float spacing, Vector3 forward)
    {
        List<Vector3> positions = new();

        forward = forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        for (int i = 0; i < count; i++)
        {
            int row = i / 2;
            int side = (i % 2 == 0) ? -1 : 1; 

            Vector3 offset = -row * spacing * forward + row * side * spacing * right;
            positions.Add(center + offset);
        }

        return positions;
    }
    private void SetupOneVsOne(int level)
    {
        // TeamA: 1 player
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        // TeamB: 1 AI
        //CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, spawnAreaTeamB.position);
        //_teamB.Add(ai);
    }

    private void SetupOneVsMany(int level)
    {
        // TeamA: 1 player
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        // TeamB: 2-5 AI
        int aiCount = 2 + Mathf.FloorToInt((level - 1) / 3); // 2 AI ở level 1-3, 3 ở 4-6, 4 ở 7-9, 5 ở 10
        var teamBPositions = GetVFormationPositions(spawnAreaTeamB.position, aiCount, 2f, Vector3.back);
        for (int i = 0; i < aiCount; i++)
        {
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i]);
            _teamB.Add(ai);
        }
    }

    private void SetupManyVsMany(int level)
    {
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position);
        _teamA.Add(player);

        int aiCountTeamA = 1 + Mathf.FloorToInt((level - 1) / 4);
        var teamAPositions = GetVFormationPositions(spawnAreaTeamA.position, aiCountTeamA, 2f, Vector3.forward);

        for (int i = 0; i < aiCountTeamA; i++)
        {
            CharacterData ai = SpawnAI(aiTeamAPool, TeamType.TeamA, level, teamAPositions[i]);
            _teamA.Add(ai);
        }

        int aiCountTeamB = aiCountTeamA + 1;
        var teamBPositions = GetVFormationPositions(spawnAreaTeamB.position, aiCountTeamB, 2f, Vector3.back);

        for (int i = 0; i < aiCountTeamB; i++)
        {
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i]);
            _teamB.Add(ai);
        }
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