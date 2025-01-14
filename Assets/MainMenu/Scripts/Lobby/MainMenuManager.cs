using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Samples;
using UnityEngine;
using MainMenu.lobby;
using MainMenu.ngo;
using Samples.HelloNetcode;

#if UNITY_EDITOR
using ParrelSync;

#endif

namespace MainMenu
{
    /// <summary>
    /// Current state of the local game.
    /// Set as a flag to allow for the Inspector to select multiple valid states for various UI features.
    /// </summary>
    [Flags]
    public enum GameState
    {
        Menu = 1,
        Lobby = 2,
        LobbyList = 4,
    }

    /// <summary>
    /// Sets up and runs the entire sample.
    /// All the Data that is important gets updated in here, the GameManager in the mainScene has all the references
    /// needed to run the game.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        public LocalLobby LocalLobby => m_LocalLobby;
        public Action<GameState> onGameStateChanged;
        public LocalLobbyList LobbyList { get; private set; } = new LocalLobbyList();

        public GameState LocalGameState { get; private set; }
        public LobbyManager LobbyManager { get; private set; }
        
        private ConnectionRelay m_connectionRelay = null;
        
        [SerializeField]
        private Countdown m_countdown;

        private LocalPlayer m_LocalUser;
        private LocalLobby m_LocalLobby;

        private vivox.VivoxSetup m_VivoxSetup = new vivox.VivoxSetup();
        [SerializeField]
        private List<vivox.VivoxUserHandler> m_vivoxUserHandlers;

        static MainMenuManager m_GameManagerInstance;

        private RelayFrontend relayFrontend;

        public int maney{ get; set; }

        public int GetManey()
        {
            return maney;
        }
        private void setManey(int value)
        {
            maney = value;
        }
        
        private void Start()
        {
            var a = GetManey();
            setManey(10);
            
            m_connectionRelay = FindObjectOfType<ConnectionRelay>();
            relayFrontend = FindObjectOfType<RelayFrontend>();
            Debug.Log("m_connectionRelay" + (m_connectionRelay != null));
            EventManager.TriggerEvent("LoadedScene");
        }

        public static MainMenuManager Instance
        {
            get
            {
                if (m_GameManagerInstance != null)
                    return m_GameManagerInstance;
                m_GameManagerInstance = FindObjectOfType<MainMenuManager>();
                return m_GameManagerInstance;
            }
        }

        /// <summary>Rather than a setter, this is usable in-editor. It won't accept an enum, however.</summary>
        

        public async Task<LocalPlayer> AwaitLocalUserInitialization()
        {
            while (m_LocalUser == null)
                await Task.Delay(100);
            return m_LocalUser;
        }

        public async void CreateLobby(string name, bool isPrivate, string password = null, int maxPlayers = 4)
        {
            try
            {
                var lobby = await LobbyManager.CreateLobbyAsync(
                    m_LocalUser.DisplayName.Value,
                    maxPlayers,
                    isPrivate,
                    m_LocalUser,
                    password);

                LobbyConverters.RemoteToLocal(lobby, m_LocalLobby);
                await CreateLobby();
            }
            catch (LobbyServiceException exception)
            {
                SetGameState(GameState.LobbyList);
                Debug.Log($"Error creating lobby : ({exception.ErrorCode}) {exception.Message}");
            }
        }

        public async void JoinLobby(string lobbyID, string lobbyCode, string password = null)
        {
            try
            {
                var lobby = await LobbyManager.JoinLobbyAsync(lobbyID, lobbyCode,
                    m_LocalUser, password:password);

                LobbyConverters.RemoteToLocal(lobby, m_LocalLobby);
                await JoinLobby();
            }
            catch (LobbyServiceException exception)
            {
                SetGameState(GameState.LobbyList);
                Debug.Log($"Error joining lobby : ({exception.ErrorCode}) {exception.Message}");
            }
        }

        public async void QueryLobbies()
        {
            LobbyList.QueryState.Value = LobbyQueryState.Fetching;
            var qr = await LobbyManager.RetrieveLobbyListAsync();
            if (qr == null)
            {
                return;
            }

            SetCurrentLobbies(LobbyConverters.QueryToLocalList(qr));
        }

        public async void QuickJoin()
        {
            var lobby = await LobbyManager.QuickJoinLobbyAsync(m_LocalUser);
            if (lobby != null)
            {
                LobbyConverters.RemoteToLocal(lobby, m_LocalLobby);
                await JoinLobby();
            }
            else
            {
                SetGameState(GameState.LobbyList);
            }
        }

        public void SetLocalUserName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.Log(
                    "Empty Name not allowed."); // Lobby error type, then HTTP error type.
                return;
            }

            m_LocalUser.DisplayName.Value = name;
            SendLocalUserData();
        }

        public void SetLocalUserEmote()
        {
            SendLocalUserData();
        }

        public void SetLocalUserStatus(PlayerStatus status)
        {
            m_LocalUser.UserStatus.Value = status;
            Debug.Log("m_LocalUser.UserStatus.Value " + m_LocalUser.UserStatus.Value);
            SendLocalUserData();
        }

        public void SetLocalLobbyColor(int color)
        {
            if (m_LocalLobby.PlayerCount < 1)
                return;
            SendLocalLobbyData();
        }

        bool updatingLobby;

        async void SendLocalLobbyData()
        {
            await LobbyManager.UpdateLobbyDataAsync(LobbyConverters.LocalToRemoteLobbyData(m_LocalLobby));
        }

        async void SendLocalUserData()
        {
            await LobbyManager.UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser));
        }

        public void UIChangeMenuState(GameState state)
        {
            var isQuittingGame = LocalGameState == GameState.Lobby &&
                m_LocalLobby.LocalLobbyState.Value == LobbyState.InGame;

            if (isQuittingGame)
            {
                //If we were in-game, make sure we stop by the lobby first
                state = GameState.Lobby;
                ClientQuitGame();
            }
            SetGameState(state);
        }

        public void HostSetRelayCode(string code)
        {
            m_LocalLobby.RelayCode.Value = code;
            SendLocalLobbyData();
        }

        //Only Host needs to listen to this and change state.
        void OnHostReady(int readyCount)
        {
            if (readyCount == 1 &&
                m_LocalLobby.LocalLobbyState.Value != LobbyState.CountDown)
            {
                m_LocalLobby.LocalLobbyState.Value = LobbyState.CountDown;
                SendLocalLobbyData();
            }
            else if (m_LocalLobby.LocalLobbyState.Value == LobbyState.CountDown)
            {
                m_LocalLobby.LocalLobbyState.Value = LobbyState.Lobby;
                SendLocalLobbyData();
            }
        }

        void OnLobbyStateChanged(LobbyState state)
        {
            if (state == LobbyState.Lobby)
                CancelCountDown();
            if (state == LobbyState.CountDown)
                BeginCountDown();
        }

        void BeginCountDown()
        {
            Debug.Log("Beginning Countdown.");
            m_countdown.StartCountDown();
        }

        void CancelCountDown()
        {
            Debug.Log("Countdown Cancelled.");
            m_countdown.CancelCountDown();
        }

        public void FinishedCountDown()
        {
            Debug.Log("FinishedCountDown()");
            m_LocalUser.UserStatus.Value = PlayerStatus.InGame;
            m_LocalLobby.LocalLobbyState.Value = LobbyState.InGame;

            StartNewGame(m_LocalLobby, m_LocalUser);
        }
        
        public async void StartNewGame(LocalLobby localLobby,LocalPlayer localPlayer)
        {
            // CoreDataHandler.instance.SetGameUID(_selectedMap);

            // save player parameters for this map
            // from the menu setup
                                                    // InitPlayerData initPlayerData = new InitPlayerData()
            // p.players = _playersData
            //     .Where((KeyValuePair<int, PlayerData> p) => _activePlayers[p.Key])
            //     .Select((KeyValuePair<int, PlayerData> p) => p.Value)
            //     .ToArray();
            // p.myPlayerId = 0;
            // p.SaveToFile($"Games/{CoreDataHandler.instance.GameUID}/PlayerParameters", true);
            
            CoreDataHandler.instance.SetLocalPlayers(localLobby.LocalPlayers);
            var index = 0;
            for (int i = 0; i < localLobby.LocalPlayers.Count; i++)
            {
                if (localLobby.LocalPlayers[i].ID.Value == localPlayer.ID.Value)
                {
                    index = i;
                }
            }
            CoreDataHandler.instance.SetMyId(index);
            CoreDataHandler.instance.SetIsHost(localPlayer.IsHost.Value);
            
            // await m_connectionRelay.StartNetworkedGame(m_LocalLobby, m_LocalUser);
            
            relayFrontend.StartNetworkedGame(m_LocalLobby, m_LocalUser);
            // CoreBooter.instance.LoadMap("GameScene");
        }

        public void BeginGame()
        {
            if (m_LocalUser.IsHost.Value)
            {
                m_LocalLobby.LocalLobbyState.Value = LobbyState.InGame;
                m_LocalLobby.Locked.Value = true;
                SendLocalLobbyData();
            }
        }

        public void ClientQuitGame()
        {
            EndGame();
            // m_setupInGame?.OnGameEnd();
        }

        public void EndGame()
        {
            if (m_LocalUser.IsHost.Value)
            {
                m_LocalLobby.LocalLobbyState.Value = LobbyState.Lobby;
                m_LocalLobby.Locked.Value = false;
                SendLocalLobbyData();
            }

            SetLobbyView();
        }
        

        #region Setup

        async void Awake()
        {
            Application.wantsToQuit += OnWantToQuit;
            m_LocalUser = new LocalPlayer("", 0, false, "LocalPlayer");
            m_LocalLobby = new LocalLobby { LocalLobbyState = { Value = LobbyState.Lobby } };
            LobbyManager = new LobbyManager();

            await InitializeServices();
            AuthenticatePlayer();
            StartVivoxLogin();
        }

        async Task InitializeServices()
        {
            string serviceProfileName = "player";
#if UNITY_EDITOR
            serviceProfileName = $"{serviceProfileName}{LocalProfileTool.LocalProfileSuffix}";
#endif
            await UnityServiceAuthenticator.TrySignInAsync(serviceProfileName);
        }

        void AuthenticatePlayer()
        {
            var localId = AuthenticationService.Instance.PlayerId;
            var randomName = NameGenerator.GetName(localId);

            m_LocalUser.ID.Value = localId;
            m_LocalUser.DisplayName.Value = randomName;
        }

        #endregion

        void SetGameState(GameState state)
        {
            var isLeavingLobby = (state == GameState.Menu || state == GameState.LobbyList) &&
                LocalGameState == GameState.Lobby;
            LocalGameState = state;

            Debug.Log($"Switching Game State to : {LocalGameState}");

            if (isLeavingLobby)
                LeaveLobby();
            onGameStateChanged.Invoke(LocalGameState);
        }

        void SetCurrentLobbies(IEnumerable<LocalLobby> lobbies)
        {
            var newLobbyDict = new Dictionary<string, LocalLobby>();
            foreach (var lobby in lobbies)
                newLobbyDict.Add(lobby.LobbyID.Value, lobby);

            LobbyList.CurrentLobbies = newLobbyDict;
            LobbyList.QueryState.Value = LobbyQueryState.Fetched;
        }

        async Task CreateLobby()
        {
            m_LocalUser.IsHost.Value = true;
            m_LocalLobby.onUserReadyChange = OnHostReady;
            try
            {
                await BindLobby();
            }
            catch (LobbyServiceException exception)
            {
                SetGameState(GameState.LobbyList);
                Debug.Log($"Couldn't join Lobby : ({exception.ErrorCode}) {exception.Message}");
            }
        }

        async Task JoinLobby()
        {
            //Trigger UI Even when same value
            m_LocalUser.IsHost.ForceSet(false);
            await BindLobby();
        }

        async Task BindLobby()
        {
            await LobbyManager.BindLocalLobbyToRemote(m_LocalLobby.LobbyID.Value, m_LocalLobby);
            m_LocalLobby.LocalLobbyState.onChanged += OnLobbyStateChanged;
            SetLobbyView();
            StartVivoxJoin();
        }

        public void LeaveLobby()
        {
            m_LocalUser.ResetState();
#pragma warning disable 4014
            LobbyManager.LeaveLobbyAsync();
#pragma warning restore 4014
            ResetLocalLobby();
            m_VivoxSetup.LeaveLobbyChannel();
            LobbyList.Clear();
        }

        void StartVivoxLogin()
        {
            m_VivoxSetup.Initialize(m_vivoxUserHandlers, OnVivoxLoginComplete);

            void OnVivoxLoginComplete(bool didSucceed)
            {
                if (!didSucceed)
                {
                    Debug.LogError("Vivox login failed! Retrying in 5s...");
                    StartCoroutine(RetryConnection(StartVivoxLogin, m_LocalLobby.LobbyID.Value));
                }
            }
        }

        void StartVivoxJoin()
        {
            m_VivoxSetup.JoinLobbyChannel(m_LocalLobby.LobbyID.Value, OnVivoxJoinComplete);

            void OnVivoxJoinComplete(bool didSucceed)
            {
                if (!didSucceed)
                {
                    Debug.LogError("Vivox connection failed! Retrying in 5s...");
                    StartCoroutine(RetryConnection(StartVivoxJoin, m_LocalLobby.LobbyID.Value));
                }
            }
        }

        IEnumerator RetryConnection(Action doConnection, string lobbyId)
        {
            yield return new WaitForSeconds(5);
            if (m_LocalLobby != null && m_LocalLobby.LobbyID.Value == lobbyId && !string.IsNullOrEmpty(lobbyId)
               ) // Ensure we didn't leave the lobby during this waiting period.
                doConnection?.Invoke();
        }

        void SetLobbyView()
        {
            Debug.Log($"Setting Lobby user state {GameState.Lobby}");
            SetGameState(GameState.Lobby);
            SetLocalUserStatus(PlayerStatus.Lobby);
        }

        void ResetLocalLobby()
        {
            m_LocalLobby.ResetLobby();
            m_LocalLobby.RelayServer = null;
        }

        #region Teardown

        /// <summary>
        /// In builds, if we are in a lobby and try to send a Leave request on application quit, it won't go through if we're quitting on the same frame.
        /// So, we need to delay just briefly to let the request happen (though we don't need to wait for the result).
        /// </summary>
        IEnumerator LeaveBeforeQuit()
        {
            ForceLeaveAttempt();
            yield return null;
            Application.Quit();
        }

        bool OnWantToQuit()
        {
            bool canQuit = string.IsNullOrEmpty(m_LocalLobby?.LobbyID.Value);
            StartCoroutine(LeaveBeforeQuit());
            return canQuit;
        }

        void OnDestroy()//need
        {
            // ForceLeaveAttempt();
            // LobbyManager.Dispose();
        }

        void ForceLeaveAttempt()
        {
            if (!string.IsNullOrEmpty(m_LocalLobby?.LobbyID.Value))
            {
#pragma warning disable 4014
                LobbyManager.LeaveLobbyAsync();
#pragma warning restore 4014
                m_LocalLobby = null;
            }
        }

        #endregion
    }
}
