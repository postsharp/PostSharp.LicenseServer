<%@ Page Title="Lease Details &mdash; SharpCrafters License Server" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Details.aspx.cs" Inherits="PostSharp.LicenseServer.Admin.DetailsPage" %>
<%@ Import Namespace="PostSharp.LicenseServer" %>
<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">

<p style="float:right">
    <a href="..">Back</a>
</p>

<h2>
    Current leases on license <asp:Label runat="server" ID="licenseIdLabel" />
</h2>

    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" 
        EnableModelValidation="True">
        <Columns>
            <asp:BoundField DataField="UserName" HeaderText="User" />
            <asp:BoundField DataField="AuthenticatedUser" HeaderText="Authenticated User" />
            <asp:BoundField DataField="Machine" HeaderText="Machine" />
            <asp:BoundField DataField="StartTime" HeaderText="Start Time (UTC)" />
            <asp:BoundField DataField="EndTime" HeaderText="End Time (UTC)" />
            <asp:TemplateField ShowHeader="False">
                <ItemTemplate>
                    <a href="Cancel.aspx?id=<%# Eval("LeaseId") %>">Cancel</a>
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
    <br />
    <p>
        <asp:Literal ID="leaseCountLiteral" runat="server" /> lease(s), 
        <asp:Literal ID="concurrentUserCountLiteral" runat="server" /> concurrent user(s).

    </p>
    
    <p>
        <asp:LinkButton ID="disableHyperlink" runat="server" Text="Disable license key" OnClick="disableHyperlink_OnClick" /> <br/>
        <asp:LinkButton ID="enableHyperlink" runat="server" Text="Enable license key" OnClick="enableHyperlink_OnClick"/> <br/>
        <asp:LinkButton ID="deleteHyperlink" runat="server" Text="Delete license key and its leases" OnClick="deleteHyperlink_OnClick" OnClientClick="return confirm('Are you certain you want to delete this license key and all associate leases?');"/>
    </p>
    
        <p>
        Page generated on <%=VirtualDateTime.UtcNow.ToLocalTime()%>.
    </p>
</asp:Content>
