﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;
using Newtonsoft.Json;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepository : KuduRemoteClientBase, IRepository
    {
        public RemoteRepository(string serviceUrl)
            :base(serviceUrl)
        {
        }

        public string CurrentId
        {
            get
            {
                return _client.GetAsync("id")
                              .Result
                              .EnsureSuccessful()
                              .Content
                              .ReadAsString();
            }
        }

        public void Initialize()
        {
            _client.PostAsync("init", new StringContent(String.Empty))
                   .Result
                   .EnsureSuccessStatusCode();
        }

        public IEnumerable<Branch> GetBranches()
        {
            return _client.GetAsyncJson<IEnumerable<Branch>>("branches");
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            return _client.GetAsyncJson<IEnumerable<FileStatus>>("status");
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            return _client.GetAsyncJson<IEnumerable<ChangeSet>>("log");
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            return _client.GetAsyncJson<IEnumerable<ChangeSet>>("log?index=" + index + "&limit=" + limit);
        }

        public ChangeSetDetail GetDetails(string id)
        {
            return _client.GetAsyncJson<ChangeSetDetail>("details/" + id);
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            return _client.GetAsyncJson<ChangeSetDetail>("working");
        }

        public void AddFile(string path)
        {
            _client.PostAsync("add", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .Result
                   .EnsureSuccessful();
        }

        public void RevertFile(string path)
        {
            _client.PostAsync("remove", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .Result
                   .EnsureSuccessful();
        }

        public ChangeSet Commit(string authorName, string message)
        {
            string json = _client.PostAsync("commit", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("name", authorName), new KeyValuePair<string, string>("message", message)))
                                 .Result
                                 .EnsureSuccessful()
                                 .Content.ReadAsString();

            return JsonConvert.DeserializeObject<ChangeSet>(json);
        }

        public void Push()
        {
            _client.PostAsync("push", new StringContent(String.Empty))
                   .Result
                   .EnsureSuccessful();
        }

        public void Update(string id)
        {
            _client.PostAsync("update", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }
    }
}