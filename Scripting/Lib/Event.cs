﻿using System;
using Microsoft.Xna.Framework;
using Neo.IronLua;

namespace BhModule.Community.Pathing.Scripting.Lib {
    public class Event {

        private const int TICK_FREQUENCY = 0;

        private readonly SafeList<Func<GameTime, LuaResult>> _tickListeners = new();

        private double _nextTick = 0;

        private readonly PathingGlobal _global;

        internal Event(PathingGlobal global) {
            _global = global;
        }

        public void OnTick(Func<GameTime, LuaResult> callback) {
            if (callback != null) {
                _tickListeners.Add(callback);
            }
        }

        internal void Update(GameTime gameTime) {
            if (gameTime.TotalGameTime.TotalMilliseconds > _nextTick) {
                _nextTick = gameTime.TotalGameTime.TotalMilliseconds + TICK_FREQUENCY;
                
                foreach (var listener in _tickListeners.ToArray()) {
                    var eventCall = _global.ScriptEngine.WrapScriptCall(() => listener(gameTime));

                    if (!eventCall.Success) {
                        // We don't allow failed listeners to remain registered.
                        _global.ScriptEngine.PushMessage($"Tick callback `{listener.Method.Name}` was unregistered because it threw an exception.", ScriptMessageLogLevel.System);
                        _tickListeners.Remove(listener);
                    }
                }
            }
        }

    }

    public class EventCallback {

        internal EventCallback(Event e, Func<GameTime, LuaResult> callback) {

        }


    }

}
