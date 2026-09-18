// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using BuildBackstageLicenseServer.Docker;
using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.TeamCity;
using PostSharp.Engineering.BuildTools.Docker;
using BackstageDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.BackstageDependencies.V2027_0;

/// <summary>
/// The continuous integration configurations that run the test suite against a database server, each one in the
/// image that carries its server.
/// </summary>
internal static class DatabaseTests
{
    public static DatabaseTestRun SqlServer { get; } = new( "sqlserver", "SqlServer", "SQL Server", new SqlServerComponent() );

    public static DatabaseTestRun PostgreSql { get; } = new( "postgresql", "PostgreSql", "PostgreSQL", new PostgreSqlComponent() );
}
