﻿using System;
using System.Reflection;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    public abstract class RustPlugin : CSharpPlugin
    {
        protected Command cmd;

        public override void SetPluginInfo(string name, string path)
        {
            base.SetPluginInfo(name, path);

            cmd = Interface.Oxide.GetLibrary<Command>();
        }

        public override void HandleAddedToManager(PluginManager manager)
        {
            #region Online Players Attribute

            foreach (var field in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = field.GetCustomAttributes(typeof(OnlinePlayersAttribute), true);
                if (attributes.Length > 0)
                {
                    var pluginField = new PluginFieldInfo(this, field);
                    if (pluginField.GenericArguments.Length != 2 || pluginField.GenericArguments[0] != typeof(BasePlayer))
                    {
                        Puts($"The {field.Name} field is not a Hash with a BasePlayer key! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.LookupMethod("Add", pluginField.GenericArguments))
                    {
                        Puts($"The {field.Name} field does not support adding BasePlayer keys! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.LookupMethod("Remove", typeof(BasePlayer)))
                    {
                        Puts($"The {field.Name} field does not support removing BasePlayer keys! (online players will not be tracked)");
                        continue;
                    }
                    if (pluginField.GenericArguments[1].GetField("Player") == null)
                    {
                        Puts($"The {pluginField.GenericArguments[1].Name} class does not have a public Player field! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.HasValidConstructor(typeof(BasePlayer)))
                    {
                        Puts($"The {field.Name} field is using a class which contains no valid constructor (online players will not be tracked)");
                        continue;
                    }
                    onlinePlayerFields.Add(pluginField);
                }
            }

            if (onlinePlayerFields.Count > 0) foreach (var player in BasePlayer.activePlayerList) AddOnlinePlayer(player);

            #endregion

            #region Command Attributes

            foreach (var method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = method.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ConsoleCommandAttribute;
                    if (attribute != null) cmd.AddConsoleCommand(attribute.Command, this, method.Name);
                    continue;
                }

                attributes = method.GetCustomAttributes(typeof(ChatCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ChatCommandAttribute;
                    if (attribute != null) cmd.AddChatCommand(attribute.Command, this, method.Name);
                }
            }

            #endregion

            base.HandleAddedToManager(manager);
        }

        #region Online Players Attribute

        [HookMethod("OnPlayerInit")]
        private void base_OnPlayerInit(BasePlayer player) => AddOnlinePlayer(player);

        [HookMethod("OnPlayerDisconnected")]
        private void base_OnPlayerDisconnected(BasePlayer player)
        {
            // Delay removing player until OnPlayerDisconnect has fired in plugin
            NextTick(() =>
            {
                foreach (var pluginField in onlinePlayerFields) pluginField.Call("Remove", player);
            });
        }

        private void AddOnlinePlayer(BasePlayer player)
        {
            foreach (var pluginField in onlinePlayerFields)
            {
                var type = pluginField.GenericArguments[1];
                var onlinePlayer = type.GetConstructor(new[] { typeof(BasePlayer) }) == null ? Activator.CreateInstance(type) : Activator.CreateInstance(type, (object)player);
                type.GetField("Player").SetValue(onlinePlayer, player);
                pluginField.Call("Add", player, onlinePlayer);
            }
        }

        #endregion
    }
}
