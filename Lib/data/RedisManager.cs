﻿using Lib.core;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Lib.extension;
using Polly.CircuitBreaker;
using System.Configuration;
using Polly;

namespace Lib.data
{
    /*
     个人觉得对NoSql数据库的操作不要使用异步，因为本身响应就很快，不会阻塞
     
     个人觉得对NoSql数据库的操作不要使用异步，因为本身响应就很快，不会阻塞
     
     个人觉得对NoSql数据库的操作不要使用异步，因为本身响应就很快，不会阻塞
         */

    /// <summary>
    /// redis 老帮助类
    /// </summary>
    public static class RedisManager
    {
        /// <summary>
        /// 10秒钟超过8次请求并且一半以上错误就熔断30秒
        /// </summary>
        private static readonly CircuitBreakerPolicy p = Policy.Handle<Exception>()
            .AdvancedCircuitBreaker(0.5, TimeSpan.FromSeconds(10), 8, TimeSpan.FromSeconds(30));

        private static readonly bool UseCircuitBreaker = 
            (ConfigurationManager.AppSettings["RedisManagerUseCircuitBreaker"] ?? "true").ToBool();

        /// <summary>
        /// 获取redis连接
        /// </summary>
        /// <param name="handler"></param>
        public static void PrepareConnection(Func<ConnectionMultiplexer, bool> handler)
        {
            if (UseCircuitBreaker)
            {
                //使用熔断器
                p.Execute(() => handler.Invoke(RedisClientManager.Instance.DefaultClient));
            }
            else
            {
                handler.Invoke(RedisClientManager.Instance.DefaultClient);
            }
        }

        /// <summary>
        /// 获取redis database
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="db_no"></param>
        public static void PrepareDataBase(Func<IDatabase, bool> callback, int? db_no = null)
        {
            PrepareConnection(con =>
            {
                IDatabase db = null;

                if (db_no != null && db_no.HasValue)
                {
                    db = con.GetDatabase(db_no.Value);
                }
                else
                {
                    db = con.GetDatabase();
                }
                callback.Invoke(db);

                return true;
            });
        }
    }

    /// <summary>
    /// redis 链接管理
    /// </summary>
    public class RedisClientManager : StaticClientManager<ConnectionMultiplexer>
    {
        public static readonly RedisClientManager Instance = new RedisClientManager();

        public override string DefaultKey
        {
            get
            {
                return ConfigHelper.Instance.RedisConnectionString;
            }
        }

        public override bool CheckClient(ConnectionMultiplexer ins)
        {
            return ins != null && ins.IsConnected;
        }

        public override ConnectionMultiplexer CreateNewClient(string key)
        {
            var pool = ConnectionMultiplexer.Connect(key);
            pool.ConnectionFailed += (sender, e) => { e.Exception?.AddErrorLog("Redis连接失败:" + e.FailureType); };
            pool.ConnectionRestored += (sender, e) => { "Redis连接恢复".AddBusinessInfoLog(); };
            pool.ErrorMessage += (sender, e) => { e.Message?.AddErrorLog("Redis-ErrorMessage"); };
            pool.InternalError += (sender, e) => { e.Exception?.AddErrorLog("Redis内部错误"); };
            return pool;
        }

        public override void DisposeClient(ConnectionMultiplexer ins)
        {
            ins?.Dispose();
        }
    }

    /// <summary>
    /// Redis操作
    /// https://github.com/qq1206676756/RedisHelp
    /// </summary>
    public class RedisHelper : SerializeBase, IDisposable
    {
        private int DbNum { get; }

        private readonly ConnectionMultiplexer _conn;

        public ConnectionMultiplexer Connection { get { return _conn; } }

        public RedisHelper(int dbNum = 1) : this(dbNum, null) { }

        public RedisHelper(int dbNum, string readWriteHosts)
        {
            DbNum = dbNum;
            _conn =
                string.IsNullOrWhiteSpace(readWriteHosts) ?
                RedisClientManager.Instance.DefaultClient :
                RedisClientManager.Instance.GetCachedClient(readWriteHosts);
        }

        #region String

        /// <summary>
        /// 保存一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <param name="expiry"></param>
        /// <returns></returns>
        public bool StringSet(string key, object obj, TimeSpan? expiry = default(TimeSpan?))
        {
            return Do(db => db.StringSet(key, Serialize(obj), expiry));
        }

        /// <summary>
        /// 获取一个key的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T StringGet<T>(string key)
        {
            return Do(db => Deserialize<T>(db.StringGet(key)));
        }

        /// <summary>
        /// 为数字增长val
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val">可以为负</param>
        /// <returns>增长后的值</returns>
        public double StringIncrement(string key, double val = 1)
        {
            return Do(db => db.StringIncrement(key, val));
        }

        /// <summary>
        /// 为数字减少val
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val">可以为负</param>
        /// <returns>减少后的值</returns>
        public double StringDecrement(string key, double val = 1)
        {
            return Do(db => db.StringDecrement(key, val));
        }
        #endregion String

        #region List

        /// <summary>
        /// 移除指定ListId的内部List的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ListRemove(string key, object value)
        {
            Do(db => db.ListRemove(key, Serialize(value)));
        }

        /// <summary>
        /// 获取指定key的List
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<T> ListRange<T>(string key)
        {
            return Do(redis =>
            {
                var values = redis.ListRange(key);
                return values.Where(x => x.HasValue).Select(x => Deserialize<T>(x)).ToList();
            });
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ListRightPush(string key, object value)
        {
            Do(db => db.ListRightPush(key, Serialize(value)));
        }

        /// <summary>
        /// 出队
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T ListRightPop<T>(string key)
        {
            return Do(db =>
            {
                var value = db.ListRightPop(key);
                return Deserialize<T>(value);
            });
        }

        /// <summary>
        /// pop或者阻塞
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="delay_ms"></param>
        /// <param name="timeout"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        private T PopOrBlock<T>(string key, int delay_ms, TimeSpan? timeout, Func<IDatabase, RedisValue> func)
        {
            return Do(db =>
            {
                var timeout_at = default(DateTime?);
                if (timeout != null)
                {
                    timeout_at = DateTime.Now.AddMilliseconds(timeout.Value.Milliseconds);
                }

                while (true)
                {
                    var value = func.Invoke(db);
                    if (!value.HasValue)
                    {
                        Thread.Sleep(delay_ms);
                        if (timeout_at != null && DateTime.Now > timeout_at.Value)
                        {
                            throw new Exception("等待超时");
                        }
                        continue;
                    }
                    return Deserialize<T>(value);
                }
            });
        }

        /// <summary>
        /// 拿不到数据就阻塞
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="delay_ms"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public T BListRightPop<T>(string key, int delay_ms = 10, TimeSpan? timeout = null)
        {
            return PopOrBlock<T>(key, delay_ms, timeout, db => db.ListRightPop(key));
        }

        /// <summary>
        /// 入栈
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void ListLeftPush(string key, object value)
        {
            Do(db => db.ListLeftPush(key, Serialize(value)));
        }

        /// <summary>
        /// 出栈
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T ListLeftPop<T>(string key)
        {
            return Do(db =>
            {
                var value = db.ListLeftPop(key);
                return Deserialize<T>(value);
            });
        }

        /// <summary>
        /// 拿不到数据就阻塞
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="delay_ms"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public T BListLeftPop<T>(string key, int delay_ms = 10, TimeSpan? timeout = null)
        {
            return PopOrBlock<T>(key, delay_ms, timeout, db => db.ListLeftPop(key));
        }

        /// <summary>
        /// 获取集合中的数量
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long ListLength(string key)
        {
            return Do(redis => redis.ListLength(key));
        }

        #endregion List

        #region key

        /// <summary>
        /// 删除单个key
        /// </summary>
        /// <param name="key">redis key</param>
        /// <returns>是否删除成功</returns>
        public bool KeyDelete(string key)
        {
            return Do(db => db.KeyDelete(key));
        }

        /// <summary>
        /// 判断key是否存储
        /// </summary>
        /// <param name="key">redis key</param>
        /// <returns></returns>
        public bool KeyExists(string key)
        {
            return Do(db => db.KeyExists(key));
        }

        /// <summary>
        /// 重新命名key
        /// </summary>
        /// <param name="key">就的redis key</param>
        /// <param name="newKey">新的redis key</param>
        /// <returns></returns>
        public bool KeyRename(string key, string newKey)
        {
            return Do(db => db.KeyRename(key, newKey));
        }

        /// <summary>
        /// 设置Key的时间
        /// </summary>
        /// <param name="key">redis key</param>
        /// <param name="expiry"></param>
        /// <returns></returns>
        public bool KeyExpire(string key, TimeSpan? expiry = default(TimeSpan?))
        {
            return Do(db => db.KeyExpire(key, expiry));
        }

        #endregion key

        #region 发布订阅

        /// <summary>
        /// Redis发布订阅  订阅
        /// </summary>
        /// <param name="subChannel"></param>
        /// <param name="handler"></param>
        public void Subscribe(string subChannel, Action<RedisChannel, RedisValue> handler = null)
        {
            var sub = _conn.GetSubscriber();
            sub.Subscribe(subChannel, (channel, message) =>
            {
                handler(channel, message);
            });
        }

        /// <summary>
        /// Redis发布订阅  发布
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="channel"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public long Publish(string channel, object msg)
        {
            var sub = _conn.GetSubscriber();
            return sub.Publish(channel, Serialize(msg));
        }

        /// <summary>
        /// Redis发布订阅  取消订阅
        /// </summary>
        /// <param name="channel"></param>
        public void Unsubscribe(string channel)
        {
            var sub = _conn.GetSubscriber();
            sub.Unsubscribe(channel);
        }

        /// <summary>
        /// Redis发布订阅  取消全部订阅
        /// </summary>
        public void UnsubscribeAll()
        {
            var sub = _conn.GetSubscriber();
            sub.UnsubscribeAll();
        }

        #endregion 发布订阅

        #region 辅助方法

        private T Do<T>(Func<IDatabase, T> func)
        {
            var database = _conn.GetDatabase(DbNum);
            return func(database);
        }

        public ITransaction CreateTransaction()
        {
            return GetDatabase().CreateTransaction();
        }

        public IDatabase GetDatabase()
        {
            return _conn.GetDatabase(DbNum);
        }

        public IServer GetServer(string hostAndPort)
        {
            return _conn.GetServer(hostAndPort);
        }

        public void Dispose()
        {
            //do nothing
        }

        #endregion 辅助方法

    }

    class MQTest
    {
        public MQTest()
        {
            var stop = false;
            RedisManager.PrepareDataBase(db =>
            {
                while (true)
                {
                    if (stop) { break; }

                    var data = db.ListRightPop("es-index");
                    if (!data.HasValue)
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                    //handler data
                    string json = data;
                    var model = json.JsonToEntity<RedisHelper>();
                }
                return true;
            });
        }
    }
}
