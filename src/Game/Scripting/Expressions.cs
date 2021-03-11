﻿#region license

// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
namespace ClassicUO.Game.Scripting
{
    public static class Expressions
    {
        // Called by upper management class to register all the desired expressions
        public static void Register()
        {
            Interpreter.RegisterExpressionHandler("findobject", ExpressionToCommand);
            Interpreter.RegisterExpressionHandler("findalias", ExpressionToCommand);
            Interpreter.RegisterExpressionHandler("findtype", ExpressionToCommand);
        }

        public static bool ExpressionToCommand(string expression, Argument[] args, bool quiet, bool force)
        {
            try
            {
                var execution = Commands.Definitions[expression].CreateExecution(args, quiet, force);
                return execution.Process();
            }
            catch (Exception ex)
            {
                if (quiet)  // We dont raise script related problems when quiet
                    return false;
                else throw ex;
            }
        }
    }
}