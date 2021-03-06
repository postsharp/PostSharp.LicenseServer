﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PostSharp.LicenseServer
{
	using System.Data.Linq;
	using System.Data.Linq.Mapping;
	using System.Data;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Linq;
	using System.Linq.Expressions;
	using System.ComponentModel;
	using System;
	
	
	[global::System.Data.Linq.Mapping.DatabaseAttribute(Name="PostSharp.LicenseServer")]
	public partial class Database : System.Data.Linq.DataContext
	{
		
		private static System.Data.Linq.Mapping.MappingSource mappingSource = new AttributeMappingSource();
		
    #region Extensibility Method Definitions
    partial void OnCreated();
    partial void InsertLicense(License instance);
    partial void UpdateLicense(License instance);
    partial void DeleteLicense(License instance);
    partial void InsertLease(Lease instance);
    partial void UpdateLease(Lease instance);
    partial void DeleteLease(Lease instance);
    #endregion
		
		public Database() : 
				base(global::System.Configuration.ConfigurationManager.ConnectionStrings["SharpCrafters_LicenseServerConnectionString"].ConnectionString, mappingSource)
		{
			OnCreated();
		}
		
		public Database(string connection) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public Database(System.Data.IDbConnection connection) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public Database(string connection, System.Data.Linq.Mapping.MappingSource mappingSource) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public Database(System.Data.IDbConnection connection, System.Data.Linq.Mapping.MappingSource mappingSource) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public System.Data.Linq.Table<License> Licenses
		{
			get
			{
				return this.GetTable<License>();
			}
		}
		
		public System.Data.Linq.Table<Lease> Leases
		{
			get
			{
				return this.GetTable<Lease>();
			}
		}
	}
	
	[global::System.Data.Linq.Mapping.TableAttribute(Name="dbo.Licenses")]
	public partial class License : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _LicenseId;
		
		private string _LicenseKey;
		
		private string _ProductCode;
		
		private int _Priority;
		
		private System.DateTime _CreatedOn;
		
		private System.Nullable<System.DateTime> _GraceStartTime;
		
		private System.Nullable<System.DateTime> _GraceLastWarningTime;
		
		private EntitySet<Lease> _Leases;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnLicenseIdChanging(int value);
    partial void OnLicenseIdChanged();
    partial void OnLicenseKeyChanging(string value);
    partial void OnLicenseKeyChanged();
    partial void OnProductCodeChanging(string value);
    partial void OnProductCodeChanged();
    partial void OnPriorityChanging(int value);
    partial void OnPriorityChanged();
    partial void OnCreatedOnChanging(System.DateTime value);
    partial void OnCreatedOnChanged();
    partial void OnGraceStartTimeChanging(System.Nullable<System.DateTime> value);
    partial void OnGraceStartTimeChanged();
    partial void OnGraceLastWarningTimeChanging(System.Nullable<System.DateTime> value);
    partial void OnGraceLastWarningTimeChanged();
    #endregion
		
		public License()
		{
			this._Leases = new EntitySet<Lease>(new Action<Lease>(this.attach_Leases), new Action<Lease>(this.detach_Leases));
			OnCreated();
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_LicenseId", DbType="Int NOT NULL", IsPrimaryKey=true)]
		public int LicenseId
		{
			get
			{
				return this._LicenseId;
			}
			set
			{
				if ((this._LicenseId != value))
				{
					this.OnLicenseIdChanging(value);
					this.SendPropertyChanging();
					this._LicenseId = value;
					this.SendPropertyChanged("LicenseId");
					this.OnLicenseIdChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_LicenseKey", DbType="Text NOT NULL", CanBeNull=false, UpdateCheck=UpdateCheck.Never)]
		public string LicenseKey
		{
			get
			{
				return this._LicenseKey;
			}
			set
			{
				if ((this._LicenseKey != value))
				{
					this.OnLicenseKeyChanging(value);
					this.SendPropertyChanging();
					this._LicenseKey = value;
					this.SendPropertyChanged("LicenseKey");
					this.OnLicenseKeyChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_ProductCode", DbType="VarChar(50) NOT NULL", CanBeNull=false)]
		public string ProductCode
		{
			get
			{
				return this._ProductCode;
			}
			set
			{
				if ((this._ProductCode != value))
				{
					this.OnProductCodeChanging(value);
					this.SendPropertyChanging();
					this._ProductCode = value;
					this.SendPropertyChanged("ProductCode");
					this.OnProductCodeChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_Priority", DbType="Int NOT NULL")]
		public int Priority
		{
			get
			{
				return this._Priority;
			}
			set
			{
				if ((this._Priority != value))
				{
					this.OnPriorityChanging(value);
					this.SendPropertyChanging();
					this._Priority = value;
					this.SendPropertyChanged("Priority");
					this.OnPriorityChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_CreatedOn", DbType="DateTime NOT NULL")]
		public System.DateTime CreatedOn
		{
			get
			{
				return this._CreatedOn;
			}
			set
			{
				if ((this._CreatedOn != value))
				{
					this.OnCreatedOnChanging(value);
					this.SendPropertyChanging();
					this._CreatedOn = value;
					this.SendPropertyChanged("CreatedOn");
					this.OnCreatedOnChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_GraceStartTime", DbType="DateTime")]
		public System.Nullable<System.DateTime> GraceStartTime
		{
			get
			{
				return this._GraceStartTime;
			}
			set
			{
				if ((this._GraceStartTime != value))
				{
					this.OnGraceStartTimeChanging(value);
					this.SendPropertyChanging();
					this._GraceStartTime = value;
					this.SendPropertyChanged("GraceStartTime");
					this.OnGraceStartTimeChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_GraceLastWarningTime", DbType="DateTime")]
		public System.Nullable<System.DateTime> GraceLastWarningTime
		{
			get
			{
				return this._GraceLastWarningTime;
			}
			set
			{
				if ((this._GraceLastWarningTime != value))
				{
					this.OnGraceLastWarningTimeChanging(value);
					this.SendPropertyChanging();
					this._GraceLastWarningTime = value;
					this.SendPropertyChanged("GraceLastWarningTime");
					this.OnGraceLastWarningTimeChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.AssociationAttribute(Name="License_Lease", Storage="_Leases", ThisKey="LicenseId", OtherKey="LicenseId")]
		public EntitySet<Lease> Leases
		{
			get
			{
				return this._Leases;
			}
			set
			{
				this._Leases.Assign(value);
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		private void attach_Leases(Lease entity)
		{
			this.SendPropertyChanging();
			entity.License = this;
		}
		
		private void detach_Leases(Lease entity)
		{
			this.SendPropertyChanging();
			entity.License = null;
		}
	}
	
	[global::System.Data.Linq.Mapping.TableAttribute(Name="dbo.Leases")]
	public partial class Lease : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _LeaseId;
		
		private System.Nullable<int> _OverwrittenLeaseId;
		
		private int _LicenseId;
		
		private System.DateTime _StartTime;
		
		private System.DateTime _EndTime;
		
		private string _UserName;
		
		private string _Machine;
		
		private string _AuthenticatedUser;
		
		private string _HMAC;
		
		private bool _Grace;
		
		private EntitySet<Lease> _OverwrittenByLease;
		
		private EntityRef<Lease> _OverwritesLease;
		
		private EntityRef<License> _License;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnLeaseIdChanging(int value);
    partial void OnLeaseIdChanged();
    partial void OnOverwrittenLeaseIdChanging(System.Nullable<int> value);
    partial void OnOverwrittenLeaseIdChanged();
    partial void OnLicenseIdChanging(int value);
    partial void OnLicenseIdChanged();
    partial void OnStartTimeChanging(System.DateTime value);
    partial void OnStartTimeChanged();
    partial void OnEndTimeChanging(System.DateTime value);
    partial void OnEndTimeChanged();
    partial void OnUserNameChanging(string value);
    partial void OnUserNameChanged();
    partial void OnMachineChanging(string value);
    partial void OnMachineChanged();
    partial void OnAuthenticatedUserChanging(string value);
    partial void OnAuthenticatedUserChanged();
    partial void OnHMACChanging(string value);
    partial void OnHMACChanged();
    partial void OnGraceChanging(bool value);
    partial void OnGraceChanged();
    #endregion
		
		public Lease()
		{
			this._OverwrittenByLease = new EntitySet<Lease>(new Action<Lease>(this.attach_OverwrittenByLease), new Action<Lease>(this.detach_OverwrittenByLease));
			this._OverwritesLease = default(EntityRef<Lease>);
			this._License = default(EntityRef<License>);
			OnCreated();
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_LeaseId", AutoSync=AutoSync.OnInsert, DbType="Int NOT NULL IDENTITY", IsPrimaryKey=true, IsDbGenerated=true)]
		public int LeaseId
		{
			get
			{
				return this._LeaseId;
			}
			set
			{
				if ((this._LeaseId != value))
				{
					this.OnLeaseIdChanging(value);
					this.SendPropertyChanging();
					this._LeaseId = value;
					this.SendPropertyChanged("LeaseId");
					this.OnLeaseIdChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_OverwrittenLeaseId", DbType="Int")]
		public System.Nullable<int> OverwrittenLeaseId
		{
			get
			{
				return this._OverwrittenLeaseId;
			}
			set
			{
				if ((this._OverwrittenLeaseId != value))
				{
					if (this._OverwritesLease.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnOverwrittenLeaseIdChanging(value);
					this.SendPropertyChanging();
					this._OverwrittenLeaseId = value;
					this.SendPropertyChanged("OverwrittenLeaseId");
					this.OnOverwrittenLeaseIdChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_LicenseId", DbType="Int NOT NULL")]
		public int LicenseId
		{
			get
			{
				return this._LicenseId;
			}
			set
			{
				if ((this._LicenseId != value))
				{
					if (this._License.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnLicenseIdChanging(value);
					this.SendPropertyChanging();
					this._LicenseId = value;
					this.SendPropertyChanged("LicenseId");
					this.OnLicenseIdChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_StartTime", DbType="DateTime NOT NULL")]
		public System.DateTime StartTime
		{
			get
			{
				return this._StartTime;
			}
			set
			{
				if ((this._StartTime != value))
				{
					this.OnStartTimeChanging(value);
					this.SendPropertyChanging();
					this._StartTime = value;
					this.SendPropertyChanged("StartTime");
					this.OnStartTimeChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_EndTime", DbType="DateTime NOT NULL")]
		public System.DateTime EndTime
		{
			get
			{
				return this._EndTime;
			}
			set
			{
				if ((this._EndTime != value))
				{
					this.OnEndTimeChanging(value);
					this.SendPropertyChanging();
					this._EndTime = value;
					this.SendPropertyChanged("EndTime");
					this.OnEndTimeChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_UserName", DbType="NVarChar(200) NOT NULL", CanBeNull=false)]
		public string UserName
		{
			get
			{
				return this._UserName;
			}
			set
			{
				if ((this._UserName != value))
				{
					this.OnUserNameChanging(value);
					this.SendPropertyChanging();
					this._UserName = value;
					this.SendPropertyChanged("UserName");
					this.OnUserNameChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_Machine", DbType="NVarChar(200) NOT NULL", CanBeNull=false)]
		public string Machine
		{
			get
			{
				return this._Machine;
			}
			set
			{
				if ((this._Machine != value))
				{
					this.OnMachineChanging(value);
					this.SendPropertyChanging();
					this._Machine = value;
					this.SendPropertyChanged("Machine");
					this.OnMachineChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_AuthenticatedUser", DbType="NVarChar(200) NOT NULL", CanBeNull=false)]
		public string AuthenticatedUser
		{
			get
			{
				return this._AuthenticatedUser;
			}
			set
			{
				if ((this._AuthenticatedUser != value))
				{
					this.OnAuthenticatedUserChanging(value);
					this.SendPropertyChanging();
					this._AuthenticatedUser = value;
					this.SendPropertyChanged("AuthenticatedUser");
					this.OnAuthenticatedUserChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_HMAC", DbType="VarChar(100)")]
		public string HMAC
		{
			get
			{
				return this._HMAC;
			}
			set
			{
				if ((this._HMAC != value))
				{
					this.OnHMACChanging(value);
					this.SendPropertyChanging();
					this._HMAC = value;
					this.SendPropertyChanged("HMAC");
					this.OnHMACChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.ColumnAttribute(Storage="_Grace", DbType="Bit NOT NULL")]
		public bool Grace
		{
			get
			{
				return this._Grace;
			}
			set
			{
				if ((this._Grace != value))
				{
					this.OnGraceChanging(value);
					this.SendPropertyChanging();
					this._Grace = value;
					this.SendPropertyChanged("Grace");
					this.OnGraceChanged();
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.AssociationAttribute(Name="Lease_Lease", Storage="_OverwrittenByLease", ThisKey="LeaseId", OtherKey="OverwrittenLeaseId")]
		public EntitySet<Lease> OverwrittenByLease
		{
			get
			{
				return this._OverwrittenByLease;
			}
			set
			{
				this._OverwrittenByLease.Assign(value);
			}
		}
		
		[global::System.Data.Linq.Mapping.AssociationAttribute(Name="Lease_Lease", Storage="_OverwritesLease", ThisKey="OverwrittenLeaseId", OtherKey="LeaseId", IsForeignKey=true)]
		public Lease OverwritesLease
		{
			get
			{
				return this._OverwritesLease.Entity;
			}
			set
			{
				Lease previousValue = this._OverwritesLease.Entity;
				if (((previousValue != value) 
							|| (this._OverwritesLease.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._OverwritesLease.Entity = null;
						previousValue.OverwrittenByLease.Remove(this);
					}
					this._OverwritesLease.Entity = value;
					if ((value != null))
					{
						value.OverwrittenByLease.Add(this);
						this._OverwrittenLeaseId = value.LeaseId;
					}
					else
					{
						this._OverwrittenLeaseId = default(Nullable<int>);
					}
					this.SendPropertyChanged("OverwritesLease");
				}
			}
		}
		
		[global::System.Data.Linq.Mapping.AssociationAttribute(Name="License_Lease", Storage="_License", ThisKey="LicenseId", OtherKey="LicenseId", IsForeignKey=true)]
		public License License
		{
			get
			{
				return this._License.Entity;
			}
			set
			{
				License previousValue = this._License.Entity;
				if (((previousValue != value) 
							|| (this._License.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._License.Entity = null;
						previousValue.Leases.Remove(this);
					}
					this._License.Entity = value;
					if ((value != null))
					{
						value.Leases.Add(this);
						this._LicenseId = value.LicenseId;
					}
					else
					{
						this._LicenseId = default(int);
					}
					this.SendPropertyChanged("License");
				}
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		private void attach_OverwrittenByLease(Lease entity)
		{
			this.SendPropertyChanging();
			entity.OverwritesLease = this;
		}
		
		private void detach_OverwrittenByLease(Lease entity)
		{
			this.SendPropertyChanging();
			entity.OverwritesLease = null;
		}
	}
}
#pragma warning restore 1591
