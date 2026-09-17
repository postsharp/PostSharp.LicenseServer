<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Graph.aspx.cs" Inherits="PostSharp.LicenseServer.Graph" %>

<asp:Content ID="Content1" ContentPlaceHolderID="Head" runat="server">
    
   <script src="http://yui.yahooapis.com/3.18.1/build/yui/yui-min.js"></script>

    <style>
        #mychart
        {
            width: 100%;
            height: 400px;
        }
    </style>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body" runat="server">

<p style="float:right">
    <a href="..">Back</a>
</p>

<h2>License <%=this.LicenseId%>: <%=this.Days%>-Days Usage History</h2>

<p>
    <a href="Graph.aspx?id=<%=this.LicenseId%>&amp;days=30">30 days</a> | 
    <a href="Graph.aspx?id=<%=this.LicenseId%>&amp;days=90">90 days</a> | 
    <a href="Graph.aspx?id=<%=this.LicenseId%>&amp;days=180">180 days</a> | 
    <a href="Graph.aspx?id=<%=this.LicenseId%>&amp;days=365">365 days</a>
    </p>

    <div id="mychart">
    </div>

    <script>
    // Create a new YUI instance and populate it with the required modules.
    YUI().use('charts', function (Y) {

        // Charts is available and ready for use.

    
    // Data for the chart
        var myDataValues = [
    <% 
    bool comma = false;
    for ( int i = 0; i < this.Keys.Length; i++)
{
    if ( comma ) 
        this.Response.Write( ",\n" ); 
    else
        comma = true;

    this.Response.Write( string.Format( "{{ date: \"{0}\", used: {1}",  this.Keys[i], this.Values[i]) );
    if ( this.Maximum.HasValue )
    {
        this.Response.Write( string.Format( ", maximum: {0}, grace: {1}", this.Maximum, this.GraceMaximum ) );
    }
    this.Response.Write( "}" );

} %>
  
];

    // Instantiate and render the chart
    var myAxes = {
        leases:{
            keys:[  <% if ( this.Maximum.HasValue ) { %> "maximum", "grace", <%}%> "used"],
            position:"right",
            type:"numeric",
            maximum: <%=this.AxisMaximum %>,
            styles:{
                majorTicks:{
                    display: "none"
                }
            }
        },
        dateRange:{
            keys:["date"],
            position:"bottom",
            type:"category",
            styles:{
                majorTicks:{
                    display: "none"
                },
                label: {
                    rotation:-45,
                    margin:{top:5}
                }
            }
        }
    };
 
    //define the series 
    var seriesCollection = [
    
          <% if ( this.Maximum.HasValue )
             {%>
        {
            type:"line",
            xAxis:"dateRange",
            yAxis:"leases",
            xKey:"date",
            yKey:"maximum",
            yDisplayName:"Authorized",
            styles:
            {
            line: { color: "#ffa500"}
            }
        },
        {
            type:"line",
            xAxis:"dateRange",
            yAxis:"leases",
            xKey:"date",
            yKey:"grace",
            yDisplayName:"Grace",
            styles:
            {
            line: { color: "#ff0000"}
            }
        },
        <%
             }%>
              {
   
            type:"line",
            xAxis:"dateRange",
            yAxis:"leases",
            xKey:"date",
            yKey:"used",
            xDisplayName:"Date",
            yDisplayName:"Used",
            styles: {
                border: {
                    weight: 1,
                    color: "#58006e"
                },
                over: {
                    fill: {
                        alpha: 0.7
                    }
                }
            }
        }
    ];
 
    //instantiate the chart
    var myChart = new Y.Chart({
                        dataProvider:myDataValues, 
                        axes:myAxes, 
                        seriesCollection:seriesCollection, 
                        horizontalGridlines: true,
                        verticalGridlines: true,
                        render:"#mychart",
                        categoryKey: "date",
                        categoryType: "time"
                    });


});
    </script>

</asp:Content>
