<%@ Page Title="Cancel Lease &mdash; SharpCrafters License Server" Language="C#"
    MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Cancel.aspx.cs"
    Inherits="PostSharp.LicenseServer.Admin.CancelPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">
    <h2>
        Cancel Lease</h2>
    <p>
        Cancelling a lease on server side does not prevent the client from using the licensed
        software until the lease has expired. You, and not the licensing server, are
        ultimately responsible to ensure that the license agreement is being respected.
    </p>
    <p>
        Are you sure you want to cancel the lease?
    </p>
    <p>
        <asp:Button Text="Yes" ID="yesButton" runat="server" OnClick=yesButton_OnClick />
        <asp:Button Text="No" ID="noButton" runat="server" OnClick=noButton_OnClick />
    </p>
</asp:Content>
