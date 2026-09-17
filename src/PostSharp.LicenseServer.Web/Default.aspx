<%@ Page Title="PostSharp License Server" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Default.aspx.cs" Inherits="PostSharp.LicenseServer.DefaultPage" %>
<%@ Import Namespace="PostSharp.LicenseServer" %>

<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">
    <h2>
        Licenses Installed</h2>
    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" 
    EnableModelValidation="True" >
        <AlternatingRowStyle BackColor="White" />
        <Columns>
            <asp:BoundField DataField="LicenseId" HeaderText="License Id" />
            <asp:BoundField DataField="ProductCode" HeaderText="Product" />
            <asp:BoundField DataField="LicenseType" HeaderText="License Type" />
            <asp:BoundField DataField="GraceStartTime" HeaderText="Grace Period Started On" />
            <asp:BoundField DataField="MaxUsers" HeaderText="Max Number of Users" />
            <asp:BoundField DataField="CurrentUsers" HeaderText="Current Number Of Users" />
            <asp:BoundField DataField="MaintenanceEndDate" HeaderText="End of Maintenance"  DataFormatString="{0:d}"/>
            <asp:BoundField DataField="Status" HeaderText="Status"/>
            <asp:HyperLinkField DataNavigateUrlFields="LicenseId" DataNavigateUrlFormatString="Admin/Details.aspx?id={0}"
                Text="Detail" />
                   <asp:HyperLinkField DataNavigateUrlFields="LicenseId" DataNavigateUrlFormatString="Graph.aspx?id={0}"
                Text="History" />
        </Columns>
       
    </asp:GridView>
    <asp:Panel ID="noLicensePanel" runat="server" Visible="false">
        <p>No license has been installed in this license server.</p>
    </asp:Panel>
    <p>
        <a href="Admin/AddLicense.aspx">Install a new license</a>
        <br />
        <a href="Admin/Export.aspx">Export logs</a>
    </p>
    
    <p>
        Page generated on <%=VirtualDateTime.UtcNow.ToLocalTime()%>.
    </p>
   
</asp:Content>
