﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Hiwjcn.Core.Domain.Auth;

namespace Hiwjcn.Web.wcf
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码和配置文件中的接口名“IAuthApi”。
    [ServiceContract]
    public interface IAuthApi
    {
        [OperationContract]
        Task<AuthTokenWcf> CreateToken();
    }
}
