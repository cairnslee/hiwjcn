﻿using Model;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebLogic.Model.User
{
    /// <summary>
    /// 角色
    /// </summary>
    [Table("account_role")]
    public class RoleModel : BaseEntity
    {
        /// <summary>
        /// 角色名
        /// </summary>
        [Column("role_name")]
        [MaxLength(20)]
        [Required]
        public virtual string RoleName { get; set; }

        /// <summary>
        /// 角色描述
        /// </summary>
        [Column("role_desc")]
        [MaxLength(500)]
        public virtual string RoleDescription { get; set; }

        /// <summary>
        /// 用户注册的时候自动分配这些权限
        /// </summary>
        [Column("auto_assign_to_user")]
        public virtual int AutoAssignRole { get; set; }
    }
}
