<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="GenerateDemoData.aspx.cs" Inherits="PostSharp.LicenseServer.Admin.GenerateDemoData" %>
<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">
    Product:
<asp:TextBox ID="ProductTextBox" runat="server"></asp:TextBox>
<br />
<asp:TextBox ID="VersionTextBox" runat="server"></asp:TextBox>
<br />
Count:
<asp:TextBox ID="CountTextBox" runat="server"></asp:TextBox>
<br />
    <asp:Button ID="Button1" runat="server" onclick="Button1_Click" Text="Generate" />
</asp:Content>
