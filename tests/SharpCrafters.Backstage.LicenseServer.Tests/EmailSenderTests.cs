// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Tests.Fakes;

namespace SharpCrafters.Backstage.LicenseServer.Tests;

/// <summary>
/// The in-memory <see cref="IEmailSender"/> used throughout the test suite, and the guarantee that
/// no test ever reaches a real SMTP server.
/// </summary>
public sealed class EmailSenderTests
{
    [Fact]
    public async Task InMemorySender_RecordsWhatItWasAskedToSend()
    {
        InMemoryEmailSender sender = new();

        await sender.SendAsync( new EmailMessage( "admin@example.com", null, "Subject", "Body" ) );

        var message = Assert.Single( sender.Sent );
        Assert.Equal( "admin@example.com", message.To );
        Assert.Equal( "Subject", message.Subject );
        Assert.Equal( "Body", message.Body );
        Assert.Null( message.Cc );
    }

    [Fact]
    public async Task InMemorySender_PreservesOrder()
    {
        InMemoryEmailSender sender = new();

        await sender.SendAsync( new EmailMessage( "a@example.com", null, "first", "" ) );
        await sender.SendAsync( new EmailMessage( "b@example.com", null, "second", "" ) );

        Assert.Equal( ["first", "second"], sender.Sent.Select( m => m.Subject ) );
        Assert.Equal( "second", sender.Last!.Subject );
    }

    [Fact]
    public async Task InMemorySender_FiltersBySubject()
    {
        InMemoryEmailSender sender = new();

        await sender.SendAsync( new EmailMessage( "a@example.com", null, "WARNING: capacity exceeded", "" ) );
        await sender.SendAsync( new EmailMessage( "a@example.com", null, "ERROR: request denied", "" ) );

        Assert.Single( sender.WithSubject( "WARNING" ) );
        Assert.Single( sender.WithSubject( "denied" ) );
        Assert.Empty( sender.WithSubject( "nothing like this" ) );
    }

    [Fact]
    public async Task InMemorySender_CanSimulateABrokenServer()
    {
        InMemoryEmailSender sender = new() { ThrowOnSend = new InvalidOperationException( "SMTP is down" ) };

        await Assert.ThrowsAsync<InvalidOperationException>( () => sender.SendAsync( new EmailMessage( "a@example.com", null, "s", "b" ) ) );

        Assert.Empty( sender.Sent );
    }

    [Fact]
    public async Task InMemorySender_Clear_DiscardsRecordedMessages()
    {
        InMemoryEmailSender sender = new();
        await sender.SendAsync( new EmailMessage( "a@example.com", null, "s", "b" ) );

        sender.Clear();

        Assert.Empty( sender.Sent );
        Assert.Null( sender.Last );
    }

    [Fact]
    public async Task NullSender_DiscardsEverything()
    {
        NullEmailSender sender = new();

        // Must not throw: this is what the demo-data generator and disabled-SMTP deployments use.
        await sender.SendAsync( new EmailMessage( "a@example.com", "b@example.com", "s", "b" ) );
    }
}