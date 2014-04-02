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
 * Date: 3/31/2014
 * Time: 6:39 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
 
using NUnit.Framework;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace net.kolotyluk.windows.elevate
{
	[TestFixture]
	public class ElevateTest
	{
		TcpListener tcpListener;
		StringBuilder stringBuilder = new StringBuilder();
		
		[Test]
		public void NoArguments()
		{
			System.Console.WriteLine("Test Output");
			var arguments = new String[0];
			
			Assert.AreNotEqual(null, arguments);
			
			var result = Elevate.Main(arguments);
			
			Assert.AreEqual(0, result);
		}

		[Test]
		public void OneArgumentTCP()
		{
			System.Diagnostics.Debug.WriteLine("Test 2");
			tcpListener = new TcpListener(IPAddress.Loopback, 0);
			
			var listenThread = new Thread(new ThreadStart(ListenForClients));
			listenThread.IsBackground = true;
			listenThread.Start();
			
			var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port.ToString();
			var arguments = new string[] {port};
			
			// Give up our time-slice so the thread can start
			Thread.Sleep(0);
			
			Assert.AreNotEqual(null, arguments);

			int result = Elevate.Main(arguments);
			
			// Give up our time-slice so the thread can finish
			Thread.Sleep(10);
			
			System.Diagnostics.Debug.WriteLine(stringBuilder);
			
			Assert.AreEqual(0, result);
			
			
		}
		
		private void ListenForClients()
		{
			tcpListener.Start();
			TcpClient client = tcpListener.AcceptTcpClient();
			var clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
			clientThread.IsBackground = true;
    		clientThread.Start(client);
		}
		
		private void HandleClient(object client)
		{
			var tcpClient = (TcpClient)client;
  			var	clientStream = tcpClient.GetStream();

			var encoder = new ASCIIEncoding();
			var message = new byte[4096];

			try
			{
				//blocks until a client sends a message
				var bytesRead = clientStream.Read(message, 0, 4096);
				while (bytesRead > 0)
				{
					//message has successfully been received
					stringBuilder.Append(encoder.GetString(message, 0, bytesRead));
					
					//blocks until a client sends a message
				    bytesRead = clientStream.Read(message, 0, 4096);
				}
			}
			finally
			{
				tcpClient.Close();
			}
		}
	}
}
