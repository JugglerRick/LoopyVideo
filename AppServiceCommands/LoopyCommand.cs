﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace LoopyVideo.Commands
{
 
    internal class LoopyCommand
    {
        private readonly static string commandName = "Command";
        private readonly static string paramName = "Param";

        public enum CommandType
        {
            Unknown,
            Play,
            Stop,
            Media
        };


        public CommandType Command { get; set; }
        public string Param { get; set; }

        public LoopyCommand()
        {
            Command = CommandType.Unknown;
            Param = string.Empty;
        }

        public static LoopyCommand FromValueSet(ValueSet values)
        {
            LoopyCommand lc = new LoopyCommand();

            if (values.ContainsKey(commandName))
            {
                lc.Command = (LoopyCommand.CommandType)Enum.Parse(typeof(LoopyCommand.CommandType), values[commandName].ToString());
            }
            if (values.ContainsKey(paramName))
            {
                lc.Param = (string)values[paramName];
            }
            return lc;
        }

        public LoopyCommand(CommandType c, string p)
        {
            Command = c;
            Param = p;
        }

        public ValueSet ToValueSet()
        {
            ValueSet ret = new ValueSet();
            ret.Add(commandName, Command.ToString());
            if (!string.IsNullOrEmpty(Param))
            {
                ret.Add(paramName, Param);
            }
            return ret;
        }

        public void AddToValueSet(ref ValueSet set)
        {
            set.Add(commandName, Command.ToString());
            if (!string.IsNullOrEmpty(Param))
            {
                set.Add(paramName, Param);
            }
        }

        public override string ToString()
        {
            return $"Command: {Command.ToString()}  Param: {Param}";
        }
    }

}
