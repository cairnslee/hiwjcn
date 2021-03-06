﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.extension;
using Lib.helper;
using Lib.mvc.user;
using Lib.mvc.auth;
using Lib.mvc.auth.validation;
using Lib.mvc;

namespace Lib.mvc.auth
{
    public static class AuthApiControllerConfig
    {
        public const string ControllerName = "Auth";
        public const string Action_AuthCodeByPassword = "AuthCodeByPassword";
        public const string Action_AuthCodeByOneTimeCode = "AuthCodeByOneTimeCode";
        public const string Action_AccessToken = "AccessToken";
        public const string Action_CheckToken = "CheckToken";
    }

    public class AuthServerConfig
    {
        public readonly string ServerUrl;
        public readonly string WcfUrl;

        public AuthServerConfig(string host) :
            this(host, $"{host?.EnsureTrailingSlash()}Service/AuthApiService.svc")
        {
            //
        }

        public AuthServerConfig(string host, string wcf_path)
        {
            void CheckUrl(string url)
            {
                if (!ValidateHelper.IsPlumpString(url))
                {
                    throw new Exception("auth服务器地址不能为空");
                }
                var cp = url.ToLower();
                if (!(cp.StartsWith("http://") || cp.StartsWith("https://")))
                {
                    throw new Exception("服务器地址必须以http或者https开头");
                }
            }

            //server root
            this.ServerUrl = ConvertHelper.GetString(host);
            CheckUrl(this.ServerUrl);

            //server wcf url
            this.WcfUrl = wcf_path;
            CheckUrl(this.WcfUrl);
            if (!this.WcfUrl.ToLower().EndsWith(".svc"))
            {
                throw new Exception("auth wcf地址必须以.svc结尾");
            }
        }

        public string ApiPath(params string[] paths)
        {
            var path = "/".Join_(paths.Where(x => ValidateHelper.IsPlumpString(x)));
            return ServerUrl.EnsureTrailingSlash() + path;
        }

        public string AuthApiPath(string action) => this.ApiPath(new string[] { AuthApiControllerConfig.ControllerName, action });

        /// <summary>
        /// 获取code
        /// </summary>
        public string CreateAuthCodeByPassword() => this.AuthApiPath(AuthApiControllerConfig.Action_AuthCodeByPassword);

        /// <summary>
        /// 获取code
        /// </summary>
        public string CreateCodeByOneTimeCode() => this.AuthApiPath(AuthApiControllerConfig.Action_AuthCodeByOneTimeCode);

        /// <summary>
        /// 获取token
        /// </summary>
        public string CreateToken() => this.AuthApiPath(AuthApiControllerConfig.Action_AccessToken);

        /// <summary>
        /// token换用户信息
        /// </summary>
        public string CheckToken() => this.AuthApiPath(AuthApiControllerConfig.Action_CheckToken);

    }

}
