﻿using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using JackboxGPT3.Extensions;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Websocket.Client;
using Websocket.Client.Models;

namespace JackboxGPT3.Games.Common
{
    public abstract class BaseJackboxClient<TRoom, TPlayer> : IJackboxClient
    {
        private const string OP_CLIENT_WELCOME = "client/welcome";
        private const string OP_CLIENT_SEND = "client/send";
        
        private const string OP_OBJECT = "object";
        private const string OP_TEXT = "text";
        
        protected abstract string KEY_ROOM { get; }
        protected abstract string KEY_PLAYER_PREFIX { get; }

        public event EventHandler<ClientWelcome> PlayerStateChanged;
        public event EventHandler<Revision<TRoom>> OnRoomUpdate;
        public event EventHandler<Revision<TPlayer>> OnSelfUpdate;

        private readonly IConfigurationProvider _configuration;
        private readonly ILogger _logger;

        private Guid _playerId = Guid.NewGuid();
        // ReSharper disable once InconsistentNaming
        protected GameState<TRoom, TPlayer> _gameState;

        private WebsocketClient _webSocket;
        private ManualResetEvent _exitEvent;
        private int _msgSeq;

        public GameState<TRoom, TPlayer> GameState => _gameState;

        protected BaseJackboxClient(IConfigurationProvider configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void Connect()
        {
            var bootstrap = new BootstrapPayload
            {
                Role = "player",
                Name = _configuration.PlayerName,
                UserId = _playerId.ToString(),
                Format = "json",
                Password = ""
            };

            var url = new Uri($"wss://{_configuration.EcastHost}/api/v2/rooms/{_configuration.RoomCode}/play?{bootstrap.AsQueryString()}");

            _logger.Debug($"Trying to connect to ecast websocket with url: {url}");

            _webSocket = new WebsocketClient(url, () =>
            {
                var nativeClient = new ClientWebSocket();
                nativeClient.Options.AddSubProtocol("ecast-v0");
                return nativeClient;
            }) {
                MessageEncoding = Encoding.UTF8,
                IsReconnectionEnabled = false
            };

            _exitEvent = new ManualResetEvent(false);

            _webSocket.MessageReceived.Subscribe(WsReceived);
            _webSocket.ReconnectionHappened.Subscribe(WsConnected);
            _webSocket.DisconnectionHappened.Subscribe(WsDisconnected);

            _webSocket.Start();
            _exitEvent.WaitOne();
        }
        
        private void ServerMessageReceived(ServerMessage<JRaw> message)
        {
            switch(message.OpCode)
            {
                case OP_TEXT:
                    var textOp = JsonConvert.DeserializeObject<TextOperation>(message.Result.ToString());
                    HandleOperation(textOp);
                    break;
                case OP_OBJECT:
                    var objOp = JsonConvert.DeserializeObject<ObjectOperation>(message.Result.ToString());
                    HandleOperation(objOp);
                    break;
            }
        }
        
        protected virtual void HandleOperation(IOperation op)
        {
            if (op.Key == $"{KEY_PLAYER_PREFIX}{_playerId}" || op.Key == $"{KEY_PLAYER_PREFIX}{_gameState.PlayerId}")
            {
                var self = JsonConvert.DeserializeObject<TPlayer>(op.Value);
                OnSelfUpdate?.Invoke(this, new Revision<TPlayer>(_gameState.Self, self));
                _gameState.Self = self;
            }
            else if (op.Key == KEY_ROOM)
            {
                var room = JsonConvert.DeserializeObject<TRoom>(op.Value);
                OnRoomUpdate?.Invoke(this, new Revision<TRoom>(_gameState.Room, room));
                _gameState.Room = room;
            }
        }

        private void WsReceived(ResponseMessage msg)
        {
            var srvMsg = JsonConvert.DeserializeObject<ServerMessage<JRaw>>(msg.Text);

            if(srvMsg.OpCode == OP_CLIENT_WELCOME)
            {
                var cw = JsonConvert.DeserializeObject<ClientWelcome>(srvMsg.Result.ToString());
                HandleClientWelcome(cw);
                PlayerStateChanged?.Invoke(this, cw);
            }

            ServerMessageReceived(srvMsg);
        }

        private void WsConnected(ReconnectionInfo inf)
        {
            _logger.Information("Connected to Jackbox games services.");
        }

        private void WsDisconnected(DisconnectionInfo inf)
        {
            _logger.Information("Disconnected from Jackbox games services.");
            _exitEvent?.Set();
        }

        private void HandleClientWelcome(ClientWelcome cw)
        {
            _gameState.PlayerId = cw.Id;
            _logger.Debug($"Client welcome message received. Player ID: {_gameState.PlayerId}");
        }

        protected void WsSend<T>(string opCode, T body)
        {
            _msgSeq++;

            var clientMessage = new ClientMessageOperation<T>
            {
                Seq = _msgSeq,
                OpCode = opCode,
                Params = body
            };

            var msg = JsonConvert.SerializeObject(clientMessage);
            _webSocket.Send(msg);
        }
        
        protected void ClientSend<T>(T req)
        {
            var cs = new ClientSendOperation<T>
            {
                From = _gameState.PlayerId,
                To = 1,
                Body = req
            };

            WsSend(OP_CLIENT_SEND, cs);
        }
    }
}
