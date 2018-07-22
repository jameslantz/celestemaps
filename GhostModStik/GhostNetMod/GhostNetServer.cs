﻿using Celeste.Mod;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost.Net {
    public class GhostNetServer : GameComponent {

        // TODO: Timeout? Auth? Proper server functionality?

        public bool IsRunning { get; protected set; } = false;

        public TcpListener ManagementListener;
        public UdpClient UpdateClient;

        // Pseudo-connection because sending / receiving data on the same machine sucks on Windows.
        public GhostNetConnection LocalConnectionToServer;

        // Used to broadcast updates.
        public GhostNetConnection UpdateConnection;

        public List<uint> OPs = new List<uint>() {
            0
        };

        // All managed player connections.
        public List<GhostNetConnection> Connections = new List<GhostNetConnection>();
        public Dictionary<IPEndPoint, GhostNetConnection> ConnectionMap = new Dictionary<IPEndPoint, GhostNetConnection>();
        public Dictionary<IPAddress, GhostNetConnection> UpdateConnectionQueue = new Dictionary<IPAddress, GhostNetConnection>();
        public Dictionary<uint, ChunkMPlayer> PlayerMap = new Dictionary<uint, ChunkMPlayer>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();
        public List<uint> KevinballPlayerIDs = new List<uint>();
        public uint LastKevinballWinner = 0;
        public uint LastKevinballLoser = 1;
        public Dictionary<uint, Vector2> KevinballScores = new Dictionary<uint, Vector2>();
        public bool ActiveKevinballMatch = false;
        public uint CurrentKevinballLevel = 0; 

        public List<ChunkMChat> ChatLog = new List<ChunkMChat>();

        public Thread ListenerThread;

        public List<GhostNetCommand> Commands = new List<GhostNetCommand>();

        public Dictionary<string, object> ModData = new Dictionary<string, object>();

        public static event Action<GhostNetServer> OnCreate;
        public event GhostNetFrameHandler OnHandle;
        public event Action<uint, ChunkMPlayer> OnDisconnect;
        // TODO: More events.

        // Allows testing a subset of GhostNetMod's functions in an easy manner.
        // STIK: Look here for testing! 
        public bool AllowLoopbackUpdates = false;

        public GhostNetServer(Game game)
            : base(game) {
            // Just in case Mono fucks up.
            RuntimeHelpers.RunClassConstructor(typeof(GhostNetCommandsStandard).TypeHandle);

            // Find all commands in all mods.
            foreach (EverestModule module in Everest.Modules)
                RegisterCommandsFromModule(module);
            // Everest 0.0.317 and older load the module before adding it to the module list.
            // This causes an issue with the commands not being registered above when running the server on load.
            if (!Everest.Modules.Contains(GhostNetModule.Instance))
                RegisterCommandsFromModule(GhostNetModule.Instance);

            OnCreate?.Invoke(this);
        }

        public void RegisterCommandsFromModule(EverestModule module) {
            foreach (Type type in module.GetType().Assembly.GetTypes()) {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    if (!typeof(GhostNetCommand).IsAssignableFrom(field.FieldType) ||
                        field.GetCustomAttribute<GhostNetCommandFieldAttribute>() == null)
                        continue;
                    GhostNetCommand cmd = field.GetValue(null) as GhostNetCommand;
                    if (cmd == null)
                        continue;
                    Commands.Add(cmd);
                }
            }
        }

        public override void Update(GameTime gameTime) {
            
            if(inKevinballStarting)
            {
                bool idle = false; 

                if(PlayerMap.ContainsKey(KevinballP1) && PlayerMap.ContainsKey(KevinballP2))
                {
                    if(PlayerMap[KevinballP1] != null && PlayerMap[KevinballP2] != null)
                    {
                        if (PlayerMap[KevinballP1].Idle || PlayerMap[KevinballP2].Idle)
                            idle = true;
                    }
                }

                if(!idle)
                    kevinballStartTimer -= Engine.DeltaTime;
                if(kevinballStartTimer <= 0)
                {
                    inKevinballStarting = false;
                    RunKevinballMatch();
                }
            }
            base.Update(gameTime);
        }

        public GhostNetCommand GetCommand(string cmdName) {
            cmdName = cmdName.ToLowerInvariant();
            for (int i = 0; i < Commands.Count; i++) {
                GhostNetCommand cmd = Commands[i];
                if (cmd.Name == cmdName)
                    return cmd;
            }
            return null;
        }

        public string FillVariables(string input, GhostNetFrame frame)
            => input
                .Replace("((player))", frame.MPlayer.Name)
                .Replace("((id))", frame.HHead.PlayerID.ToString())
                .Replace("((server))", GhostNetModule.Settings.ServerNameAuto);

        public ChunkMChat CreateMChat(GhostNetFrame frame, string text, string tag = null, Color? color = null, bool fillVars = false, uint? id = null) {
            lock (ChatLog) {
                ChunkMChat chunk = new ChunkMChat {
                    ID = id ?? (uint) ChatLog.Count,
                    Text = fillVars ? FillVariables(text, frame) : text,
                    Tag = tag ?? "",
                    Color = color ?? GhostNetModule.Settings.ServerColorDefault,
                    Date = DateTime.UtcNow,
                    CreatedByServer = true,
                    Logged = true
                };
                if (id == null)
                    ChatLog.Add(chunk);
                else
                    ChatLog[(int) id] = chunk;
                return chunk;
            }
        }

        public void Request<T>(int playerID, out T chunk, long timeout = 5000) where T : IChunk {
            GhostNetFrame response = Request<T>(playerID, timeout);
            if (response != null) {
                response.Get(out chunk);
                return;
            }
            chunk = default(T);
        }
        public void Request<T>(GhostNetConnection con, out T chunk, long timeout = 5000) where T : IChunk {
            GhostNetFrame response = Request<T>(con, timeout);
            if (response != null) {
                response.Get(out chunk);
                return;
            }
            chunk = default(T);
        }
        public GhostNetFrame Request<T>(int playerID, long timeout = 5000) where T : IChunk
            => Request<T>(Connections[playerID], timeout);
        public GhostNetFrame Request<T>(GhostNetConnection con, long timeout = 5000) where T : IChunk
            => Request(typeof(T), con, timeout);
        public GhostNetFrame Request(Type type, GhostNetConnection con, long timeout = 5000) {
            GhostNetFrame response = null;

            // Temporary handler to grab the response.
            GhostNetFrameHandler filter = (filterCon, filterFrame) => {
                if (response != null)
                    return; // Already received a response.
                if (filterCon != con)
                    return; // Not the player we sent the request to.
                if (!filterFrame.Has(type))
                    return; // Doesn't contain the awaited response.

                response = filterFrame;
            };
            OnHandle += filter;

            // Send a request.
            con.SendManagement(new GhostNetFrame {
                new ChunkHHead {
                    PlayerID = int.MaxValue,
                },
                new ChunkMRequest {
                    ID = GhostNetFrame.GetChunkID(type)
                }
            }, true);

            // Wait for the response.
            Stopwatch timeoutWatch = new Stopwatch();
            timeoutWatch.Start();
            while (response == null && timeoutWatch.ElapsedMilliseconds < timeout)
                Thread.Sleep(0);

            // If we still get a response after the timeout elapsed but before the handler has been removed, deal with it.
            OnHandle -= filter;

            return response;
        }

        #region Management Connection Listener

        protected virtual void ListenerLoop() {
            while (IsRunning) {
                Thread.Sleep(0);

                while (ManagementListener.Pending()) {
                    // Updates are handled via WorldUpdateConnection.
                    // Receive management updates in a separate connection.
                    TcpClient client = ManagementListener.AcceptTcpClient();
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{Connections.Count} ({client.Client.RemoteEndPoint}) connected");
                    Accept(new GhostNetRemoteConnection(
                        client,
                        null
                    ) {
                        OnReceiveManagement = HandleM,
                        OnDisconnect = HandleDisconnect
                    });
                }
            }
        }

        public virtual void Accept(GhostNetConnection con) {
            uint id = (uint) Connections.Count;
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{id} ({con.ManagementEndPoint}) accepted");
            Connections.Add(con);
            ConnectionMap[con.ManagementEndPoint] = con;
            UpdateConnectionQueue[con.ManagementEndPoint.Address] = con;
            con.SendManagement(new GhostNetFrame {
                new ChunkHHead {
                    PlayerID = id
                },
                new ChunkMServerInfo {
                    Name = GhostNetModule.Settings.ServerNameAuto
                },
                new ChunkMRequest {
                    ID = ChunkMPlayer.ChunkID
                }
            }, true);
        }

        #endregion

        #region Frame Handlers

        protected virtual void SetNetHead(GhostNetConnection con, GhostNetFrame frame) {
            frame.HHead = new ChunkHHead {
                PlayerID = (uint) Connections.IndexOf(con)
            };

            // Prevent MServerInfo from being propagated.
            frame.Remove<ChunkMServerInfo>();
        }

        public virtual void Handle(GhostNetConnection con, GhostNetFrame frame) {
            SetNetHead(con, frame);

            if (frame.HHead == null)
                return;

            bool lockedMPlayer = false;

            if (frame.MPlayer != null) {
                Monitor.Enter(frame.MPlayer, ref lockedMPlayer);
                frame.MPlayer.IsCached = false;
                HandleMPlayer(con, frame);
            }

            ChunkMPlayer player;
            if (!PlayerMap.TryGetValue(frame.HHead.PlayerID, out player) || player == null) {
                // Ghost not managed - ignore the frame.
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unexpected frame from #{frame.HHead?.PlayerID.ToString() ?? "???"} ({con.ManagementEndPoint}) - no MPlayer on this connection, possibly premature");
                return;
            }
            // Temporarily attach the MPlayer chunk to make player identification easier.
            if (frame.MPlayer == null) {
                frame.MPlayer = player;
                Monitor.Enter(frame.MPlayer, ref lockedMPlayer);
                frame.MPlayer.IsCached = true;
            }

            if (frame.Has<ChunkMRequest>()) {
                // TODO: Handle requests by client in server.

                frame.Remove<ChunkMRequest>(); // Prevent request from being propagated.
            }

            if (frame.Has<ChunkMEmote>())
                HandleMEmote(con, frame);

            if (frame.Has<ChunkMChat>())
                HandleMChat(con, frame);

            if (frame.UUpdate != null)
                HandleUUpdate(con, frame);

            if (frame.Has<ChunkUActionCollision>())
                HandleUActionCollision(con, frame);

            if (frame.Has<ChunkUTouchPress>())
                HandleUTouchPress(con, frame);

            if (frame.Has<ChunkUControlPress>())
                HandleUControlPress(con, frame);

            if (frame.Has<ChunkUCrushHit>())
                HandleUCrushHit(con, frame);

            if (frame.Has<ChunkUSpikeTrigger>())
                HandleUSpikeTrigger(con, frame);

            if (frame.Has<ChunkUPlayerDeath>())
                HandleUPlayerDeath(con, frame);

            if (frame.Has<ChunkUPickupKevinRefill>())
                HandleUKevinRefill(con, frame);

            if (frame.Has<ChunkULoadedKevinball>())
                HandleULoadedKevinball(con, frame);

            if (frame.Has<ChunkUKevinballWin>())
                HandleUWonKevinball(con, frame);

            // TODO: Restrict players from abusing UAudioPlay and UParticles propagation.
            if (frame.Has<ChunkUAudioPlay>()) {
                // Propagate audio to all active players in the same room.
                frame.PropagateU = true;
            }

            if (frame.Has<ChunkUParticles>()) {
                // Propagate particles to all active players in the same room.
                frame.PropagateU = true;
            }

            OnHandle?.Invoke(con, frame);

            if (frame.PropagateM)
                PropagateM(frame);
            else if (frame.PropagateU)
                PropagateU(frame);

            if (lockedMPlayer)
                Monitor.Exit(frame.MPlayer);
        }

        public virtual void HandleMPlayer(GhostNetConnection con, GhostNetFrame frame) {
            frame.MPlayer.Name = frame.MPlayer.Name.Replace("*", "").Replace("\r", "").Replace("\n", "").Trim();
            if (frame.MPlayer.Name.Length > GhostNetModule.Settings.ServerMaxNameLength)
                frame.MPlayer.Name = frame.MPlayer.Name.Substring(0, GhostNetModule.Settings.ServerMaxNameLength);

            if (string.IsNullOrWhiteSpace(frame.MPlayer.Name))
                frame.MPlayer.Name = "#" + frame.HHead.PlayerID;

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} {frame.MPlayer.Name} in {frame.MPlayer.SID} {(char) ('A' + frame.MPlayer.Mode)} {frame.MPlayer.Level}");

            // Propagate status to all other players.
            frame.MPlayer.IsEcho = true;
            frame.PropagateM = true;

            if (!PlayerMap.ContainsKey(frame.HHead.PlayerID)) {
                // Player just connected.
                if (!string.IsNullOrWhiteSpace(GhostNetModule.Settings.ServerMessageGreeting)) {
                    BroadcastMChat(frame, GhostNetModule.Settings.ServerMessageGreeting, fillVars: true);
                    SendMChat(con, frame, GhostNetModule.Settings.ServerMessageMOTD, fillVars: true);
                }
            }

            // Inform the player about all existing ghosts.
            lock (PlayerMap) {
                PlayerMap[frame.HHead.PlayerID] = frame.MPlayer;
                foreach (KeyValuePair<uint, ChunkMPlayer> otherStatus in PlayerMap) {
                    if (otherStatus.Value == null || (!AllowLoopbackUpdates && otherStatus.Key == frame.HHead.PlayerID))
                        continue;
                    con.SendManagement(new GhostNetFrame {
                        HHead = new ChunkHHead {
                            PlayerID = otherStatus.Key
                        },

                        MPlayer = otherStatus.Value.Clone() as ChunkMPlayer
                    }, true);
                }
            }
        }

        public virtual void HandleMEmote(GhostNetConnection con, GhostNetFrame frame) {
            ChunkMEmote emote = frame;
            // Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} emote: {frame.MEmote.Value}");

            emote.Value = emote.Value.Trim();
            if (emote.Value.Length > GhostNetModule.Settings.ServerMaxEmoteValueLength)
                emote.Value = emote.Value.Substring(0, GhostNetModule.Settings.ServerMaxEmoteValueLength);

            if (GhostNetEmote.IsText(emote.Value)) {
                frame.Add(CreateMChat(frame, emote.Value, color: GhostNetModule.Settings.ServerColorEmote));
            }

            frame.PropagateM = true;
        }

        public virtual void HandleMChat(GhostNetConnection con, GhostNetFrame frame) {
            ChunkMChat msg = frame;
            msg.Text = msg.Text.TrimEnd();
            // Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} said: {frame.MChat.Text}");

            if (!msg.Logged) {
                lock (ChatLog) {
                    msg.ID = (uint) ChatLog.Count;
                    ChatLog.Add(msg);
                }
            }

            // Handle commands if necessary.
            if (msg.Text.StartsWith(GhostNetModule.Settings.ServerCommandPrefix)) {
                // Echo the chat chunk separately.
                msg.Color = GhostNetModule.Settings.ServerColorCommand;
                con.SendManagement(new GhostNetFrame {
                    frame.HHead,
                    msg
                }, true);

                GhostNetCommandEnv env = new GhostNetCommandEnv {
                    Server = this,
                    Connection = con,
                    Frame = frame
                };

                string prefix = GhostNetModule.Settings.ServerCommandPrefix;

                // TODO: This is basically a port of disbot-neo's Handler.

                string cmdName = env.Text.Substring(prefix.Length);
                cmdName = cmdName.Split(GhostNetCommand.CommandNameDelimiters)[0].ToLowerInvariant();
                if (cmdName.Length == 0)
                    return;

                GhostNetCommand cmd = GetCommand(cmdName);
                if (cmd != null) {
                    GhostNetFrame cmdFrame = frame;
                    Task.Run(() => {
                        try {
                            cmd.Parse(env);
                        } catch (Exception e) {
                            SendMChat(con, cmdFrame, $"Command {cmdName} failed: {e.Message}", color: GhostNetModule.Settings.ServerColorError, fillVars: false);
                            if (e.GetType() != typeof(Exception)) {
                                Logger.Log(LogLevel.Warn, "ghostnet-s", $"cmd failed: {env.Text}");
                                e.LogDetailed();
                            }
                        }
                    });

                } else {
                    SendMChat(con, frame, $"Command {cmdName} not found!", color: GhostNetModule.Settings.ServerColorError, fillVars: false);
                }

                return;
            }

            if (!msg.CreatedByServer) {
                msg.Text.Replace("\r", "").Replace("\n", "");
                if (msg.Text.Length > GhostNetModule.Settings.ServerMaxChatTextLength)
                    msg.Text = msg.Text.Substring(0, GhostNetModule.Settings.ServerMaxChatTextLength);

                msg.Tag = "";
                msg.Color = Color.White;
            }

            msg.Date = DateTime.UtcNow;

            frame.PropagateM = true;
        }

        public virtual void HandleUUpdate(GhostNetConnection con, GhostNetFrame frame) {
            // Prevent unordered outdated frames from being handled.
            uint lastIndex;
            if (GhostIndices.TryGetValue(frame.HHead.PlayerID, out lastIndex) && frame.UUpdate.UpdateIndex < lastIndex) {
                // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Out of order update from #{frame.HHead.PlayerID} ({con.UpdateEndPoint}) - got {frame.UUpdate.UpdateIndex}, newest is {lastIndex}");
                return;
            }
            GhostIndices[frame.HHead.PlayerID] = frame.UUpdate.UpdateIndex;

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received UUpdate from #{frame.HHead.PlayerID} ({con.UpdateEndPoint})");

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUActionCollision(GhostNetConnection con, GhostNetFrame frame) {
            // Allow outdated collision frames to be handled.

            ChunkUActionCollision collision = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(collision.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            ) {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUTouchPress(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUTouchPress press = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(press.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUKevinRefill(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUPickupKevinRefill press = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(press.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }


        public virtual void HandleUControlPress(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUControlPress press = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(press.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUCrushHit(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUCrushHit hit = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(hit.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUSpikeTrigger(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUSpikeTrigger trigger = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(trigger.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUPlayerDeath(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUPlayerDeath press = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(press.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        public virtual void HandleUWonKevinball(GhostNetConnection con, GhostNetFrame frame)
        {
            // Allow outdated press frames to be handled.

            ChunkUKevinballWin win = frame;

            TryEndKevin(win.Winner, win.Loser, win.WinType);
        }

        public void TryEndKevin(uint winner, uint loser, uint wintype)
        {
            if (!ActiveKevinballMatch)
                return;

            ActiveKevinballMatch = false;
            EndKevinballMatch(winner, loser, wintype);
        }

        public virtual void HandleULoadedKevinball(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkULoadedKevinball press = frame;

            ChunkMPlayer otherPlayer;
            if (!PlayerMap.TryGetValue(press.With, out otherPlayer) || otherPlayer == null ||
                frame.MPlayer.SID != otherPlayer.SID ||
                frame.MPlayer.Mode != otherPlayer.Mode
            )
            {
                // Player not in the same room.
                return;
            }

            if(!KevinballPlayerIDs.Contains(press.With))
            {
                KevinballPlayerIDs.Add(press.With);
                KevinballQueue.Add(press.With);
            }

            if(!KevinballScores.ContainsKey(press.With))
            {
                KevinballScores.Add(press.With, new Vector2(0, 0));
            }

            if (ActiveKevinballMatch)
                return; 

            if(KevinballPlayerIDs.Count - SpectatingPlayers.Count >= 2)
            {
                StartKevinball(con, frame);
            }
        }

        public float kevinballStartTimer = 3f;
        public bool inKevinballStarting = false;
        public int lastPotIndex = 0;
        public bool firstKevinball = true;
        public List<uint> KevinballQueue = new List<uint>();
        public List<uint> SpectatingPlayers = new List<uint>();
        public uint KevinballP1 = 0;
        public uint KevinballP2 = 0; 

        public void StartKevinball(GhostNetConnection con, GhostNetFrame frame)
        {
            if (inKevinballStarting)
                return;

            kevinballStartTimer = 3f;
            inKevinballStarting = true; 
            BroadcastMChat(frame, "Starting Kevinball match in 3 seconds...");

            uint player1 = 0; 
            uint player2 = 0; 

            if (firstKevinball || !KevinballPlayerIDs.Contains(LastKevinballWinner) || KevinballPlayerIDs.Count == 2 || KevinballQueue.Count == 0)
            {
                player1 = KevinballPlayerIDs[0];
                player2 = KevinballPlayerIDs[1];
                firstKevinball = false;
                KevinballQueue = new List<uint>(KevinballPlayerIDs);
                KevinballQueue.Remove(player1);
                KevinballQueue.Remove(player2);
            }
            else
            {
                player1 = LastKevinballWinner;
                KevinballQueue.Add(LastKevinballLoser);
                player2 = KevinballQueue[0];
                KevinballQueue.RemoveAt(0);
            }

            KevinballP1 = player1;
            KevinballP2 = player2; 
        }

        public void RunKevinballMatch()
        {
            if (KevinballPlayerIDs.Count < 2)
                return;

            if (ActiveKevinballMatch)
                return;

            uint player1 = KevinballP1;
            uint player2 = KevinballP2;
            ActiveKevinballMatch = true;
            CurrentGameTicker++; 

            if(ShuffleMode > 0)
            {
                if(CurrentGameTicker >= ShuffleMode)
                {
                    CurrentGameTicker = 0;
                    CurrentKevinballLevel = CurrentKevinballLevel + 1; 
                }
            }

            if (!PlayerMap.ContainsKey(player1) || !PlayerMap.ContainsKey(player2) || !KevinballScores.ContainsKey(player1) || !KevinballScores.ContainsKey(player2))
                return;

            string p1Name = PlayerMap[player1].Name;
            string p2Name = PlayerMap[player2].Name;
            string p1Score = KevinballScores[player1].X.ToString() + " - " + KevinballScores[player1].Y.ToString();
            string p2Score = KevinballScores[player2].X.ToString() + " - " + KevinballScores[player2].Y.ToString();
            //BroadcastMChat(new GhostNetFrame
            //{
            //    HHead = new ChunkHHead
            //    {
            //        PlayerID = uint.MaxValue;
            //},
            //}, "Starting Kevinball!" + p1Name + " [" + p1Score + "] vs. " + p2Name + " [" + p2Score + "]");

            string finalString = "Starting Kevinball! " + p1Name + " [" + p1Score + "] vs. " + p2Name + " [" + p2Score + "]";

            BroadcastMChat(new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            }, finalString );

            GhostNetFrame frame = new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            };

            ChunkMKevinballStart chunk = new ChunkMKevinballStart
            {
                Player1 = player1,
                Player2 = player2,
                NextLevel = CurrentKevinballLevel
            };

            frame.Add(chunk);
            PropagateM(frame);


            //Send Kevinball start chunk with both player ids 

        }

        public uint ShuffleMode = 0;
        public uint CurrentGameTicker = 0; 

        public void SetShuffle(int mode)
        {
            if(mode == 0)
            {
                if (ShuffleMode == 0)
                    mode = 5;
                else
                    mode = 0; 
            }
            ShuffleMode = (uint)mode;

            string str = "Shuffle mode set to " + ShuffleMode.ToString(); 

            BroadcastMChat(new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            }, str);
        }

        public void SetLevel(int id)
        {
            if (id == -1)
                CurrentKevinballLevel++;
            else
                CurrentKevinballLevel = (uint)id;

            BroadcastMChat(new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            }, "Level changed");
        }

        public void SetSpec(uint id)
        {
            if (id == KevinballP1 || id == KevinballP2)
                return; 

            if(SpectatingPlayers.Contains(id))
            {
                SpectatingPlayers.Remove(id);
                KevinballQueue.Add(id);
            }
            else
            {
                SpectatingPlayers.Add(id);
                KevinballQueue.Remove(id);
            }
        }

        public void EndKevinballMatch(uint winner, uint loser, uint wintype)
        {
            ActiveKevinballMatch = false;
            LastKevinballWinner = winner;
            LastKevinballLoser = loser;
            KevinballScores[winner] = new Vector2(KevinballScores[winner].X + 1, KevinballScores[winner].Y);
            KevinballScores[loser] = new Vector2(KevinballScores[loser].X, KevinballScores[loser].Y + 1);

            if (!PlayerMap.ContainsKey(winner))
                return;

            string finalString = PlayerMap[winner].Name + " wins!";

            if (wintype == GhostNetClient.KevinballWin)
                finalString = finalString + " GOOOOAAAAALLLL!!";
            else if (wintype == GhostNetClient.CoinWin)
                finalString = finalString + " Coin victory!";
            else if (wintype == GhostNetClient.DeathWin)
                finalString = finalString + " Survival victory!";

            BroadcastMChat(new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            }, finalString);

            GhostNetFrame frame = new GhostNetFrame
            {
                HHead = new ChunkHHead
                {
                    PlayerID = uint.MaxValue
                }
            };

            ChunkMKevinballEnd chunk = new ChunkMKevinballEnd
            {
                Winner = winner,
                Wintype = wintype
            };

            frame.Add(chunk);
            PropagateM(frame);
        }

        #endregion

        #region Frame Senders

        public void PropagateM(GhostNetFrame frame) {
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null)
                    continue;
                otherCon.SendManagement(frame, false);
            }
        }

        public void PropagateU(GhostNetFrame frame) {
            // U is always handled after M. Even if sending this fails, we shouldn't worry about loosing M chunks.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null || (!AllowLoopbackUpdates && i == frame.HHead.PlayerID))
                    continue;

                ChunkMPlayer otherPlayer;
                if (!PlayerMap.TryGetValue((uint) i, out otherPlayer) || otherPlayer == null ||
                    frame.MPlayer.SID != otherPlayer.SID ||
                    frame.MPlayer.Mode != otherPlayer.Mode
                ) {
                    continue;
                }

                if (!(otherCon is GhostNetRemoteConnection)) {
                    otherCon.SendUpdate(frame, false);
                } else if (otherCon.UpdateEndPoint != null && !GhostNetModule.Settings.SendUFramesInMStream) {
                    UpdateConnection.SendUpdate(frame, otherCon.UpdateEndPoint, false);
                } else {
                    // Fallback for UDP-less clients.
                    otherCon.SendManagement(frame, false);
                }
            }
        }

        public ChunkMChat BroadcastMChat(GhostNetFrame frame, string text, string tag = null, Color? color = null, bool fillVars = false) {
            ChunkMChat msg = CreateMChat(frame, text, tag, color ?? GhostNetModule.Settings.ServerColorBroadcast, fillVars);
            PropagateM(new GhostNetFrame {
                new ChunkHHead {
                    PlayerID = uint.MaxValue
                },
                msg
            });
            return msg;
        }

        public ChunkMChat SendMChat(GhostNetConnection con, GhostNetFrame frame, string text, string tag = null, Color? color = null, bool fillVars = false) {
            ChunkMChat msg = CreateMChat(frame, text, tag, color, fillVars);
            con.SendManagement(new GhostNetFrame {
                new ChunkHHead {
                    PlayerID = uint.MaxValue
                },
                msg
            }, true);
            return msg;
        }

        #endregion

        #region Connection Handlers

        protected virtual void HandleM(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            // We can receive frames from LocalConnectionToServer, which isn't "valid" when we want to send back data.
            // Get the management connection to the remote client.
            if (con == null || !ConnectionMap.TryGetValue(remote, out con) || con == null)
                return;

            // If we received an update via the managed con, forget about the update con.
            if (frame.UUpdate != null) {
                if (con.UpdateEndPoint != null) {
                    ConnectionMap[con.UpdateEndPoint] = null;
                    ConnectionMap[con.ManagementEndPoint] = con; // In case Managed == Update
                    con.UpdateEndPoint = null;
                } else {
                    UpdateConnectionQueue[con.ManagementEndPoint.Address] = null;
                }
            } else {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = con;
            }

            Handle(con, frame);
        }

        protected virtual void HandleU(GhostNetConnection conReceived, IPEndPoint remote, GhostNetFrame frame, bool log) {
            // Prevent UpdateConnection locking in on a single player.
            if (conReceived == UpdateConnection)
                UpdateConnection.UpdateEndPoint = null;

            GhostNetConnection con;
            // We receive updates either from LocalConnectionToServer or from UpdateConnection.
            // Get the managed connection to the remote client.
            if (conReceived == null || !ConnectionMap.TryGetValue(remote, out con) || con == null) {
                // Unlike management connections, which we already know the target port of at the time of connection,
                // updates are sent via UDP (by default) and thus "connectionless."
                // If we've got a queued connection for that address, update it.
                GhostNetConnection queue;
                if (UpdateConnectionQueue.TryGetValue(remote.Address, out queue) && queue != null) {
                    con = queue;
                    con.UpdateEndPoint = remote;
                    ConnectionMap[con.UpdateEndPoint] = con;
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Mapped update connection ({con.ManagementEndPoint}, {con.UpdateEndPoint})");
                } else {
                    // If the address is completely unknown, drop the frame.
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unknown update from {remote} - unknown connection, possibly premature");
                    return;
                }
            }

            Handle(con, frame);
        }

        protected virtual void HandleDisconnect(GhostNetConnection con) {
            if (!ConnectionMap.TryGetValue(con.ManagementEndPoint, out con) || con == null)
                return; // Probably already disconnected.

            uint id = (uint) Connections.IndexOf(con);
            if (id == uint.MaxValue) {
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #? ({con.ManagementEndPoint}) disconnected?");
                return;
            }
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{id} ({con.ManagementEndPoint}) disconnected");

            Connections[(int) id] = null;

            if(KevinballPlayerIDs.Contains(id))
            {
                KevinballPlayerIDs.Remove(id);
            }

            if(KevinballScores.ContainsKey(id))
            {
                KevinballScores.Remove(id);
            }

            if (KevinballQueue.Contains(id))
            {
                KevinballQueue.Remove(id);
            }

            ConnectionMap[con.ManagementEndPoint] = null;

            if (con.UpdateEndPoint != null) {
                ConnectionMap[con.UpdateEndPoint] = null;
            } else {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = null;
            }

            ChunkMPlayer player;
            if (PlayerMap.TryGetValue(id, out player) && player != null &&
                !string.IsNullOrWhiteSpace(player.Name) &&
                !string.IsNullOrWhiteSpace(GhostNetModule.Settings.ServerMessageLeave)) {
                BroadcastMChat(new GhostNetFrame {
                    HHead = new ChunkHHead {
                        PlayerID = id
                    },

                    MPlayer = player
                }, GhostNetModule.Settings.ServerMessageLeave, fillVars: true);
            }

            OnDisconnect?.Invoke(id, player);

            // Propagate disconnect to all other players.
            GhostNetFrame frame = new GhostNetFrame {
                HHead = new ChunkHHead {
                    PlayerID = id
                },

                MPlayer = new ChunkMPlayer {
                    Name = "",
                    SID = "",
                    Mode = AreaMode.Normal,
                    Level = ""
                }
            };
            lock (PlayerMap) {
                PlayerMap[id] = null;
            }
            PropagateM(frame);
        }

        #endregion

        public void Start() {
            if (IsRunning) {
                Logger.Log(LogLevel.Warn, "ghostnet-s", "Server already running, restarting");
                Stop();
            }

            Logger.Log(LogLevel.Info, "ghostnet-s", "Starting server");
            IsRunning = true;

            ManagementListener = new TcpListener(IPAddress.Any, GhostNetModule.Settings.Port);
            ManagementListener.Start();

            UpdateClient = new UdpClient(GhostNetModule.Settings.Port);
            UpdateConnection = new GhostNetRemoteConnection(
                null,
                UpdateClient
            ) {
                OnReceiveUpdate = HandleU
            };

            // Fake connection for any local clients running in the same instance.
            LocalConnectionToServer = new GhostNetLocalConnection {
                OnReceiveManagement = HandleM,
                OnReceiveUpdate = HandleU,
                OnDisconnect = HandleDisconnect
            };

            ListenerThread = new Thread(ListenerLoop);
            ListenerThread.IsBackground = true;
            ListenerThread.Start();
        }

        public void Stop() {
            if (!IsRunning)
                return;
            Logger.Log(LogLevel.Info, "ghostnet-s", "Stopping server");
            IsRunning = false;

            ListenerThread.Join();

            ManagementListener.Stop();

            // Close all management connections.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection connection = Connections[i];
                if (connection == null)
                    continue;
                connection.Dispose();
                Connections[i] = null;
            }

            UpdateConnection.Dispose();

            LocalConnectionToServer.Dispose();

            Celeste.Instance.Components.Remove(this);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }

    }
}
