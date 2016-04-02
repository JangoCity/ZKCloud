﻿using Microsoft.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZKCloud.Domain.Models;
using ZKCloud.Domain.Repositories;

namespace ZKCloud.Web.Apps.Base.src.Entities
{
    public class GenericLinkDataIndex :EntityBase {
		/// <summary>
		/// 索引ID
		/// </summary>
		public virtual long Id { get; set; }
		/// <summary>
		/// 实体类型
		/// </summary>
		public virtual string Type { get; set; }
		/// <summary>
		/// 实体ID
		/// </summary>
		public virtual long EntityId { get; set; }
		/// <summary>
		/// 分类ID
		/// </summary>
		public virtual long ClassId { get; set; }
	}

	public class GenericClassIndexCreator : IModelCreator {
		public void CreateModel(ModelBuilder modelBuilder) {
			modelBuilder.Entity<GenericLinkDataIndex>(d =>
			{
				d.ToTable("Common_GenericLinkDataIndex");
				d.HasKey(e => e.Id);
				d.Property(c => c.Type);
				d.Property(c => c.EntityId);
				d.Property(c => c.ClassId);
			});
		}
	}
}
