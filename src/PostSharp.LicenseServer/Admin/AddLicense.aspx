<%@ Page Title="Add License &mdash; PostSharp License Server" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="AddLicense.aspx.cs" Inherits="PostSharp.LicenseServer.Admin.AddLicensePage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">
    <p>
        License key:</p>
    <asp:TextBox ID="licenseKeyTextBox" Name="licenseKeyTextBox" runat="server" Rows="4" Width="500" TextMode="MultiLine"/>
    <br />
    <asp:Label ID="errorLabel" ForeColor="Red" runat="server" />
    <br />
    <asp:Button ID="AddButton" runat="server" Text="Add" OnClick="AddButton_OnClick" />
</asp:Content>
