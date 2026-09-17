<%@ Page Title="Export &mdash; SharpCrafters License Server" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="Export.aspx.cs" Inherits="PostSharp.LicenseServer.Admin.ExportPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">
    <h2>
        Log Export</h2>
    <p>
        From
        <asp:DropDownList ID="fromMonthDropDownList" runat="server">
            <asp:ListItem Value="1"></asp:ListItem>
            <asp:ListItem Value="2"></asp:ListItem>
            <asp:ListItem Value="3"></asp:ListItem>
            <asp:ListItem Value="4"></asp:ListItem>
            <asp:ListItem Value="5"></asp:ListItem>
            <asp:ListItem Value="6"></asp:ListItem>
            <asp:ListItem Value="7"></asp:ListItem>
            <asp:ListItem Value="8"></asp:ListItem>
            <asp:ListItem Value="9"></asp:ListItem>
            <asp:ListItem Value="10"></asp:ListItem>
            <asp:ListItem Value="11"></asp:ListItem>
            <asp:ListItem Value="12"></asp:ListItem>
        </asp:DropDownList>
        &nbsp;
        <asp:TextBox ID="fromYearTextBox" runat="server" runat="server" Width="72px" />
        to
        <asp:DropDownList ID="toMonthDropDownList" runat="server">
            <asp:ListItem Value="1"></asp:ListItem>
            <asp:ListItem Value="2"></asp:ListItem>
            <asp:ListItem Value="3"></asp:ListItem>
            <asp:ListItem Value="4"></asp:ListItem>
            <asp:ListItem Value="5"></asp:ListItem>
            <asp:ListItem Value="6"></asp:ListItem>
            <asp:ListItem Value="7"></asp:ListItem>
            <asp:ListItem Value="8"></asp:ListItem>
            <asp:ListItem Value="9"></asp:ListItem>
            <asp:ListItem Value="10"></asp:ListItem>
            <asp:ListItem Value="11"></asp:ListItem>
            <asp:ListItem Value="12"></asp:ListItem>
        </asp:DropDownList>
        &nbsp;
        <asp:TextBox ID="toYearTextBox" runat="server" runat="server" Width="72px" />
        &nbsp;
        <asp:Button Text="Download" ID="downloadButton" runat="server" OnClick="downloadButton_OnClick" />
    </p>
    <p>
        <asp:RequiredFieldValidator ID="fromYearTextBoxRequiredFieldValidator" runat="server"
            ControlToValidate="fromYearTextBox" ErrorMessage="From year: this field is required."></asp:RequiredFieldValidator>
        <asp:RangeValidator ID="fromYearRangeValidator" runat="server" ControlToValidate="fromYearTextBox"
            ErrorMessage="From year: invalid value." MaximumValue="2100" MinimumValue="2010"></asp:RangeValidator>
        <asp:RequiredFieldValidator ID="toYearTextBoxRequiredFieldValidator" runat="server"
            ControlToValidate="toYearTextBox" ErrorMessage="From year: this field is required."></asp:RequiredFieldValidator>
        <asp:RangeValidator ID="toYearRangeValidator" runat="server" ControlToValidate="toYearTextBox"
            ErrorMessage="From year: invalid value." MaximumValue="2100" MinimumValue="2010"></asp:RangeValidator></p>
            <p>
                <a href="..">Back</a>
            </p>
</asp:Content>
