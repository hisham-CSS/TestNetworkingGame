using System;
using System.Net;
using Bomberman.Core.Game;
using Bomberman.App.States;

namespace Bomberman.App.States
{
    /// <summary>
    /// Factory for creating IGameState instances with all required dependencies injected.
    /// Centralizes state creation logic.
    /// </summary>
    public class StateFactory
    {
        private GameContext _context;
        private GameStateManager _manager;

        public StateFactory(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public MenuState CreateMenu(string? message = null) 
            => new MenuState(_context, _manager, message);

        public LobbyState CreateLobby(bool isHost, IPEndPoint? hostEndpoint) 
            => new LobbyState(_context, _manager, isHost, hostEndpoint);

        public ServerBrowserState CreateServerBrowser() 
            => new ServerBrowserState(_context, _manager);

        public ReplaySelectState CreateReplaySelect() 
            => new ReplaySelectState(_context, _manager);

        public PromptState CreatePrompt(string message, Action onConfirm) 
            => new PromptState(_context, _manager, message, onConfirm);

        public PlayState CreatePlay(GameSession session, int localPlayerId) 
            => new PlayState(_context, _manager, session, localPlayerId);

        public PlayState CreatePlay(int localPlayerId, int totalPlayers, int seed, IPEndPoint?[] lobbySlots) 
            => new PlayState(_context, _manager, localPlayerId, totalPlayers, seed, lobbySlots);

        public PlayState CreateReplay(GameSession session) 
            => new PlayState(_context, _manager, session);

        public GameOverState CreateGameOver(GameSession session, int winnerId, bool isReplayView, bool isGameCompleted, string? endReason = null) 
            => new GameOverState(_context, _manager, session, winnerId, isReplayView, isGameCompleted, endReason);
    }
}
