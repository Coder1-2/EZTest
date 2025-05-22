using System;
using System.Collections.Generic;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum GameMode { OneVsOne, OneVsMany, ManyVsMany }


public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Spawn Areas")]
    [SerializeField] private Transform spawnAreaTeamA;
    [SerializeField] private Transform spawnAreaTeamB;
    [SerializeField] private Vector3 spawnAreaSize = new(4, 0, 4);

    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Joystick joystick;
    [SerializeField] private DynamicTextManager dynamicTextManager;
    [SerializeField] private EffectManager effectManager;
    [SerializeField] private UIManager uiManager;

    private GameSettings _gameSettings;
    private readonly int _initialAITeamAPoolSize = 24;
    private readonly int _initialAITeamBPoolSize = 25;
    private bool _isGameRunning;
    private GameMode _gameMode;
    private int _remainTime; 

    private List<CharacterData> _teamA = new();
    private List<CharacterData> _teamB = new();

    private CharacterData _playerInstance;

    private Queue<CharacterData> aiTeamAPool = new();
    private Queue<CharacterData> aiTeamBPool = new();

    public int RemainTime => _remainTime;
    public GameMode GameMode => _gameMode;
    public List<CharacterData> TeamA => _teamA;
    public List<CharacterData> TeamB => _teamB;

    public Action<TeamType> OnGameOver;
    public event Action OnTimeChanged;

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

        _gameSettings = Resources.Load<GameSettings>("GameSettings");
    }

    private void Start()
    {
        AudioManager.Instance.PlayMusic(AudioName.HomeBG);
        InitializePlayer();
        InitializeAIPools();
    }

    private void InitializePlayer()
    {
        _playerInstance = Instantiate(_gameSettings.player);
        _playerInstance.gameObject.SetActive(false);
    }

    private void InitializeAIPools()
    {
        for (int i = 0; i < _initialAITeamAPoolSize; i++)
        {
            var obj = Instantiate(_gameSettings.AITeamA);
            obj.gameObject.SetActive(false);
            aiTeamAPool.Enqueue(obj);
        }

        for (int i = 0; i < _initialAITeamBPoolSize; i++)
        {
            var obj = Instantiate(_gameSettings.AITeamB);
            obj.gameObject.SetActive(false);
            aiTeamBPool.Enqueue(obj);
        }
    }

    public void CreateText(Vector3 pos, string text)
    {
        dynamicTextManager.CreateText(pos, text, _gameSettings.textData);
    }

    public void CreateEffectHit(Vector3 pos)
    {
        effectManager.CreateEffectHit(pos);
    }

    public void SetupGame(GameMode mode, int level)
    {
        AudioManager.Instance.PlayMusic(AudioName.GameBG);

        _gameMode = mode;

        _isGameRunning = true;

        ClearTeams();

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
    }
    private void Update()
    {
        if (!_isGameRunning) return;

        var teamACount = _teamA.Count;
        for (var i = 0; i < teamACount; i++)
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

        _playerInstance.UpdateCharacter();
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

    public async UniTask DelayReturnToPool(CharacterData character, TeamType team, float delay = 2)
    {
        if (team == TeamType.TeamA)
        {
            if (_teamA.Count <= 1)
            {
                GameOver(TeamType.TeamB);
            }
        }
        else
        {
            if (_teamB.Count <= 1)
            {
                GameOver(TeamType.TeamA);
            }
        }
        await UniTask.Delay((int)(delay * 1000)); 
        ReturnToPool(character, team);
    }

    private void ClearTeams()
    {
        foreach (var character in _teamA)
        {
            if (character != null)
            {
                character.gameObject.SetActive(false);
                if (character is AIData)
                    aiTeamAPool.Enqueue(character);
            }
        }

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
        float minDistance = 0.5f;

        if (areaSize.x <= 0 || areaSize.z <= 0)
        {
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
                    break;
                }

                attempts++;
                if (attempts >= maxAttempts)
                {
                    positions.Add(position);
                    break;
                }
            } while (true);
        }

        return positions;
    }

    private void SetupOneVsOne(int level)
    {
        var levels = _gameSettings.levelConfig.oneVsOneLevels;
        OneVsOneLevelData levelData = levels[level];

        // Sinh người chơi
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position, level);
        _teamA.Add(player);

        // Sinh AI TeamB
        CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, spawnAreaTeamB.position, levelData.aiLevel);
        _teamB.Add(ai);

        // Thiết lập giới hạn thời gian nếu có
        if (levelData.timeLimit > 0)
        {
            StartTimeLimit((int)levelData.timeLimit);
        }
    }

    private void SetupOneVsMany(int level)
    {
        var levels = _gameSettings.levelConfig.oneVsManyLevels;
        OneVsManyLevelData levelData = levels[level];

        // Sinh người chơi
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position, level);
        _teamA.Add(player);

        // Sinh AI TeamB
        int aiCount = levelData.aiTeamB.aiQuantity;
        var teamBPositions = GetRandomSpawnPositions(spawnAreaTeamB.position, aiCount, spawnAreaSize);
        for (int i = 0; i < aiCount; i++)
        {
            AILevel aiLevel = DetermineAILevel(levelData.aiTeamB.easyRate, levelData.aiTeamB.mediumRate, levelData.aiTeamB.hardRate);
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i], aiLevel);
            _teamB.Add(ai);
        }

        if (levelData.timeLimit > 0)
        {
            StartTimeLimit((int)levelData.timeLimit);
        }
    }

    private void SetupManyVsMany(int level)
    {
        var levels = _gameSettings.levelConfig.manyVsManyLevels;
        ManyVsManyLevelData levelData = levels[level];

        // Sinh người chơi
        CharacterData player = SpawnPlayer(spawnAreaTeamA.position, level);
        _teamA.Add(player);

        // Sinh AI TeamA
        int aiCountTeamA = levelData.aiTeamA.aiQuantity;
        var teamAPositions = GetRandomSpawnPositions(spawnAreaTeamA.position, aiCountTeamA, spawnAreaSize);
        for (int i = 0; i < aiCountTeamA; i++)
        {
            AILevel aiLevel = DetermineAILevel(levelData.aiTeamA.easyRate, levelData.aiTeamA.mediumRate, levelData.aiTeamA.hardRate);
            CharacterData ai = SpawnAI(aiTeamAPool, TeamType.TeamA, level, teamAPositions[i], aiLevel);
            _teamA.Add(ai);
        }

        // Sinh AI TeamB
        int aiCountTeamB = levelData.aiTeamB.aiQuantity;
        var teamBPositions = GetRandomSpawnPositions(spawnAreaTeamB.position, aiCountTeamB, spawnAreaSize);
        for (int i = 0; i < aiCountTeamB; i++)
        {
            AILevel aiLevel = DetermineAILevel(levelData.aiTeamB.easyRate, levelData.aiTeamB.mediumRate, levelData.aiTeamB.hardRate);
            CharacterData ai = SpawnAI(aiTeamBPool, TeamType.TeamB, level, teamBPositions[i], aiLevel);
            _teamB.Add(ai);
        }

        if (levelData.timeLimit > 0)
        {
            StartTimeLimit((int)levelData.timeLimit);
        }
    }

    private CharacterData SpawnPlayer(Vector3 position, int level)
    {
        if (_playerInstance == null)
        {
            Debug.LogError("Player instance is not initialized!");
            return null;
        }

        _playerInstance.gameObject.SetActive(true);
        _playerInstance.transform.position = position;
        _playerInstance.transform.rotation = Quaternion.identity;

        _playerInstance.Init(level, TeamType.TeamA, AILevel.Easy);
        _playerInstance.SetJoystick(joystick);

        virtualCamera.LookAt = _playerInstance.transform;
        virtualCamera.Follow = _playerInstance.transform;

        return _playerInstance;
    }

    private CharacterData SpawnAI(Queue<CharacterData> pool, TeamType team, int level, Vector3 position, AILevel aiLevel)
    {
        CharacterData ai;

        if (pool.Count > 0)
        {
            ai = pool.Dequeue();
            ai.gameObject.SetActive(true);
        }
        else
        {
            var prefab = team == TeamType.TeamA ? _gameSettings.AITeamA : _gameSettings.AITeamB;
            ai = Instantiate(prefab);
        }

        ai.transform.SetPositionAndRotation(position, Quaternion.identity);
        ai.Init(level, team, aiLevel); 

        effectManager.CreateEffectHit(position); 
        return ai;
    }

    private AILevel DetermineAILevel(float easyRate, float mediumRate, float hardRate)
    {
        float[] weights = new float[] { easyRate, mediumRate, hardRate };
        float totalWeight = weights[0] + weights[1] + weights[2];
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float sum = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            sum += weights[i];
            if (randomValue <= sum)
            {
                return (AILevel)i;
            }
        }
        return AILevel.Medium;
    }
    private void GameOver(TeamType teamType)
    {
        _isGameRunning = false;
        OnGameOver?.Invoke(teamType);
        uiManager.ShowPanelResult(teamType).Forget();
    }
    private async void StartTimeLimit(int time)
    {
        _remainTime = time; 
        OnTimeChanged?.Invoke(); 

        while (_remainTime > 0 && _isGameRunning)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());
            if (!_isGameRunning) break; 

            _remainTime -= 1; 
            OnTimeChanged?.Invoke(); 

            if (_remainTime <= 0)
            {
                GameOver(TeamType.TeamB); // TeamB thắng mặc định
                break;
            }
        }
    }
}