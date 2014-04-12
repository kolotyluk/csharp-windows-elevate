/*	Copyright © 2014 by Eric Kolotyluk <eric@kolotyluk.net>

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
*/

/*
 * Created by SharpDevelop.
 * User: Eric
 * Date: 2/19/2014
 * Time: 12:57 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace net.kolotyluk.windows.elevate
{
	/// <summary>
	///   Run any command or program with elevate priviledges.
	///   <para>
	///     <b>Warning:</b> This program in inherently dangerous and a security risk, so don't
	///     use it without considering the security aspects of what you are doing.
	///   </para>
	/// </summary>
	/// <remarks>
	///   Note: for this program to work properly, you need to create the following app.manifest
	///   and embed it in the elevate.exe file.
	/// <code>
	/// &lt;?xml version="1.0" encoding="UTF-8" standalone="yes"?>
	/// &lt;assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
	///	  &lt;trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
	///	    &lt;security>
	///	      &lt;requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
	///	        &lt;!--
	///	          The presence of the "requestedExecutionLevel" node will disable
	///	          file and registry virtualization on Vista.
	///	
	///	          Use the "level" attribute to specify the User Account Control level:
	///	            asInvoker            = Never prompt for elevation
	///	            requireAdministrator = Always prompt for elevation
	///	            highestAvailable     = Prompt for elevation when started by administrator,
	///	              but do not prompt for administrator password when started by standard user.
	///	        -->
	///        &lt;requestedExecutionLevel level="requireAdministrator"/>
	///	      &lt;/requestedPrivileges>
	///	    &lt;/security>
	///	  &lt;/trustInfo>
	/// &lt;/assembly>
	/// </code>
	/// <para>
	///   This program is nessecitated by the serious inconvenience that Windows UAC imposes
	///   on software developers. This is exacerbated but the number of simple utility actions
	///   developers often need to do that get interrupted by UAC.
	/// </para>
	/// <para>
	///   This program was written in response to trying to write some basic automated unit tests
	///   for another program. The author needed to create some symbolic links in the file system
	///   as part of configuring the test fixtures necessary to run the tests. Unfortunately in
	///   Windows, creating symbolic links is a security risk so this can only be done via an
	///   environment running as Administrator.
	/// </para>
	/// <para>
	///   This version of the program is too general and powerfull, but it is the first version.
	///   Better versions of the program would only do specific things, such as "mklink" that only
	///   creates symbolic links instead of running arbitrary code.
	/// </para>
	/// </remarks>
	public class Elevate
	{
		/// <summary>
		/// The main entry point for the program.
		/// </summary>
		/// <param name="commandArguments">the command/program to run and its argumetns</param>
		/// <returns>exitCode where 0 is a normal exit</returns>
		/// <remarks>
		/// If the first argument is parsed as an integer, it is taken to be a TCP port on localhost.
		/// The program will emit messages, and command output there so that the calling program can
		/// monitor the progress of the execution. This is important because when programs are running
		/// elevate, they do so in a separate shell using the Administrator account and environment.
		/// Consequently, standard I/O such as stdOut and stdErr are not accesible for communication.
		/// </remarks>
		public static int Main(string[] commandArguments)
		{
			if (commandArguments == null) return -1;
			
			int commandIndex = 0;
			
			int port = 0;
				
			var commandList = new List<String>();

			if (commandArguments.Length > 0 && int.TryParse(commandArguments[0], out port))
			{
				commandIndex++;
				commandList.Add(commandArguments[0]);
			}

			commandList.Add("cmd");
			commandList.Add("/c");
			commandList.Add("mklink");
			
			for(int commandArgumentIndex = commandIndex; commandArgumentIndex < commandArguments.Length; commandArgumentIndex++)
				commandList.Add(commandArguments[commandArgumentIndex]);

			return ElevateCommon.Run(commandList.ToArray());
		}
	}
}