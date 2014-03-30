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
	class Elevate
	{
		public const string LogSource = "net.kolotyluk.windows.elevate";
		public const string LogName = "Application";
		
		static EventLog eventLog;
		
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
			int exitCode = -1;	// assume the worst, because it's Windows
			
			// Create the source, if it does not already exist. 
        	if(!EventLog.SourceExists(LogSource))
        	{
             	//An event log source should not be created and immediately used. 
             	//There is a latency time to enable the source, it should be created 
             	//prior to executing the application that uses the source. 
             	//Execute this sample a second time to use the new source.
            	EventLog.CreateEventSource(LogSource, LogName);
            	Console.WriteLine("CreatedEventSource");
				Thread.Sleep(2000);
        	}

        	// Create an EventLog instance and assign its source.
        	eventLog = new EventLog(LogName);
			eventLog.Source = LogSource;
			
			if (commandArguments.Length == 0)
			{
				// Try to explain to the end user the important facts...
				// Try to format it to fit nicely inside the tiny command
				// windows that pops up when the application is running elevated.
				
				var message = 
					"The purpose of this program is to run commands using elevated priviledge\n(i.e. Run As Administrator).\n" +
					"\nIf you don't really know what you are doing, then think twice about doing it.\n" +
					"You have been warned!\n" +
					"\nuseage:  elevate {port} command {arg1, arg2, ...}\n" +
					"\nexample: elevate cmd /c mklink /D up ..\n" +
					"\nTo start a cmd shell to create a symbolic link from up to .. (the parent directory)\n" +
					"Messages are logged to:\n" +
					"    Computer Management\n" +
					"      System Tools\n" +
					"        Event Viewer\n" +
					"          Windows Logs\n" +
					"            " + LogName + "\n" +
					"              " + LogSource + "\n" +
					"\nIf the first command argument is an integer, it is takend to be\n" +
					"the port number on localhost to also log messages to. For example:\n" +
					"\n\televate 12345 cmd /c mklink /D up ..\n" +
					"\nto create a sumbolic link, and log status via TCP on port 12345.\n";
	
				Console.WriteLine(message);
				
				pause("not null");
			}
			else
			{
				TcpClient client = null;
				NetworkStream networkStream = null;
				StreamReader streamReader = null;
				StreamWriter streamWriter = null;
				
				Console.WriteLine("step 1");
				
				Process process = null;
				
				try
	     		{
					int commandIndex = 0;
					
					int port;
					
					if (int.TryParse(commandArguments[0], out port))
					{
						commandIndex++;
					}
					
					if (port > 0)
					{
						client = new TcpClient("localhost", port);
						networkStream = client.GetStream();
            			streamReader = new StreamReader(networkStream);
            			streamWriter = new StreamWriter(networkStream);
            			streamWriter.AutoFlush = true;
						eventLog.WriteEntry("Using localhost:" + port + " for TCP/IP communication");
					}
					
				 	Console.WriteLine("step 2");
					// Create a new process with the commandArguments we are given.

					var stringBuilder = new StringBuilder();
					for(int commandArgumentIndex = commandIndex + 1; commandArgumentIndex < commandArguments.Length; commandArgumentIndex++)
						stringBuilder.Append(commandArguments[commandArgumentIndex]).Append(" ");
					
					String processArguments = stringBuilder.ToString();
					
					var message = "Elevating: " + commandArguments[commandIndex] + " " + processArguments;
					Console.WriteLine(message);
					if (streamWriter != null) streamWriter.WriteLine(message);
					eventLog.WriteEntry(message);
					
					// Now we create a process, assign its ProcessStartInfo and start it
	    			process = new System.Diagnostics.Process();
					process.StartInfo = new System.Diagnostics.ProcessStartInfo(commandArguments[commandIndex], processArguments);
					
					// The following commands are needed to redirect the standard output.
	    			// This means that it will be redirected to the Process.StandardOutput StreamReader.
					
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.CreateNoWindow = true;
					
					Console.WriteLine("step 3");
					
					// Start the process and read any output from it.
					if (process.Start())
					{
						if (process.WaitForExit(10000)) // 10 seconds
						{
							exitCode = process.ExitCode;

							if (exitCode == 0)
							{
								// Get the output into a string
	    						var outputString = process.StandardOutput.ReadToEnd();
								emit(outputString, streamWriter);
							}
							else
							{
								emit("Process exited with exitCode = " + exitCode, streamWriter, EventLogEntryType.Error);
								pause(networkStream);
							}		
						}
						else
						{
							emit("Timed out after 10 seconds waiting for process to exit.", streamWriter, EventLogEntryType.Error);
							pause(networkStream);
						}
					}
					else
					{
						emit("The Process did not start as expected.", streamWriter, EventLogEntryType.Error);
						pause(networkStream);
					}
	      		}
				catch (SocketException socketException)
				{
					emit("SocketException: " + socketException.Message, streamWriter, EventLogEntryType.Error);
					pause(networkStream);
				}
	      		catch (Exception exception)
	      		{
					emit(exception + 
						"\nDirectory.GetCurrentDirectory() = " + Directory.GetCurrentDirectory() +
						"\nprocess.StartInfo.FileName = " + process.StartInfo.FileName, streamWriter, EventLogEntryType.Error);
					pause(networkStream);
	      		}
				finally
				{
					// Clean up nicely so the other end of the socket
					// does not get a rude (connection reset) surprise.
					if (streamWriter != null) streamWriter.Flush();
					if (networkStream != null) networkStream.Close();
				}
			}

			return exitCode;
		}
		
		static void emit(String message, TextWriter textWriter)
		{
			emit(message, textWriter, EventLogEntryType.Information);
		}
		
		static void emit(String message, TextWriter textWriter, EventLogEntryType eventLogEntryType)
		{
			eventLog.WriteEntry(message, eventLogEntryType);
			Console.WriteLine("The Process did not start as expected.");
			if (textWriter != null) textWriter.WriteLine(message);
		}
		
		static void pause(object networkStream)
		{
			// Don't pause if there is a network stream, because we don't want to hand our caller.
			if (networkStream == null)
			{
				Console.Write("Press any key to continue . . . ");
				Console.ReadKey(true);
			}
		}
	}
}