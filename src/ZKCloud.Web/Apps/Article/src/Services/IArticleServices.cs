﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZKCloud.Domain.Models;
using ZKCloud.Domain.Services;


namespace ZKCloud.Web.Apps.Article.src.Domains.Services {
	/// <summary>
	/// 文章数据仓储
	/// </summary>
	public interface IArticleServices : IAutoApiService {
		/// <summary>
		/// 添加文章
		/// </summary>
		/// <param name="article"></param>
		void AddSingle(Entities.Article article);
		/// <summary>
		/// 返回文章列表
		/// </summary>
		/// <returns></returns>
		List<Entities.Article> GetList();
		/// <summary>
		/// 编辑文章
		/// </summary>
		/// <param name=""></param>
		void UpdateSingle(Entities.Article article);
		/// <summary>
		/// 获取文章
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		Entities.Article Read(long id);
		/// <summary>
		/// 删除文章，用‘，’隔开Id
		/// </summary>
		/// <param name="ids"></param>
		void Delete(params int[] ids);

		IList<Entities.Article> ReadMany(); 

		PagedList<Entities.Article> ReadPage(int pageIndex=1,int pageSize=10);
	}
}
