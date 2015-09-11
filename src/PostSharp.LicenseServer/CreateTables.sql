
/****** Object:  Table [dbo].[Licenses]    Script Date: 04/05/2011 16:32:09 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Licenses](
	[LicenseId] [int] NOT NULL,
	[LicenseKey] [text] NOT NULL,
	[ProductCode] [varchar](50) NOT NULL,
	[Priority] [int] NOT NULL,
	[CreatedOn] [datetime] NOT NULL,
	[GraceStartTime] [datetime] NULL,
	[GraceLastWarningTime] [datetime] NULL,
 CONSTRAINT [PK_Licenses] PRIMARY KEY CLUSTERED 
(
	[LicenseId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
/****** Object:  Table [dbo].[Leases]    Script Date: 04/05/2011 16:32:09 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[Leases](
	[LeaseId] [int] IDENTITY(1,1) NOT NULL,
	[OverwrittenLeaseId] [int] NULL,
	[LicenseId] [int] NOT NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NOT NULL,
	[UserName] [nvarchar](200) NOT NULL,
	[Machine] [nvarchar](200) NOT NULL,
	[AuthenticatedUser] [nvarchar](200) NOT NULL,
	[HMAC] [varchar](100) NULL,
	[Grace] [bit] NOT NULL,
 CONSTRAINT [PK_Leases] PRIMARY KEY CLUSTERED 
(
	[LeaseId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
CREATE NONCLUSTERED INDEX [IX_Leases_EndTime] ON [dbo].[Leases] 
(
	[EndTime] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Leases_OverwrittenLeaseId] ON [dbo].[Leases] 
(
	[OverwrittenLeaseId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
/****** Object:  StoredProcedure [dbo].[CountActiveLeases]    Script Date: 04/05/2011 16:32:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[CountActiveLeases]( @LicenseId int, @MachinesPerUser int, @Time datetime, @Count  int output )
AS
BEGIN
	SELECT @Count = ISNULL(SUM(CEILING(machines/(cast( @MachinesPerUser as float)))), 0) 
FROM   (SELECT leases.username username,  COUNT(*)                                  machines 
        FROM   leases 
               LEFT OUTER JOIN leases AS overwriteleases 
                 ON overwriteleases.overwrittenleaseid = leases.leaseid 
        WHERE overwriteleases.LeaseId IS NULL
			   AND leases.starttime <= @Time
               AND leases.endtime >= @Time
               AND leases.LicenseId = @LicenseId
        GROUP  BY leases.username) p 

END
GO
/****** Object:  Default [DF_Licenses_Priority]    Script Date: 04/05/2011 16:32:09 ******/
ALTER TABLE [dbo].[Licenses] ADD  CONSTRAINT [DF_Licenses_Priority]  DEFAULT ((0)) FOR [Priority]
GO
/****** Object:  Default [DF_Licenses_CreatedOn]    Script Date: 04/05/2011 16:32:09 ******/
ALTER TABLE [dbo].[Licenses] ADD  CONSTRAINT [DF_Licenses_CreatedOn]  DEFAULT (getdate()) FOR [CreatedOn]
GO
/****** Object:  Default [DF_Leases_Grace]    Script Date: 04/05/2011 16:32:09 ******/
ALTER TABLE [dbo].[Leases] ADD  CONSTRAINT [DF_Leases_Grace]  DEFAULT ((0)) FOR [Grace]
GO
/****** Object:  ForeignKey [FK_Leases_Leases]    Script Date: 04/05/2011 16:32:09 ******/
ALTER TABLE [dbo].[Leases]  WITH CHECK ADD  CONSTRAINT [FK_Leases_Leases] FOREIGN KEY([OverwrittenLeaseId])
REFERENCES [dbo].[Leases] ([LeaseId])
GO
ALTER TABLE [dbo].[Leases] CHECK CONSTRAINT [FK_Leases_Leases]
GO
/****** Object:  ForeignKey [FK_Leases_Licenses]    Script Date: 04/05/2011 16:32:09 ******/
ALTER TABLE [dbo].[Leases]  WITH CHECK ADD  CONSTRAINT [FK_Leases_Licenses] FOREIGN KEY([LicenseId])
REFERENCES [dbo].[Licenses] ([LicenseId])
GO
ALTER TABLE [dbo].[Leases] CHECK CONSTRAINT [FK_Leases_Licenses]
GO
