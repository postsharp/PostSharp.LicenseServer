using System;
using PostSharp.Extensibility;

namespace SharpCrafters.LicenseServer.Test
{
    internal class MessageSink : IMessageSink
    {
        public void Write( Message message )
        {
            Console.WriteLine(message.MessageText);
        }
    }
}