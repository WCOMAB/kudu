﻿#region License

// Copyright 2010 Jeremy Skinner (http://www.jeremyskinner.co.uk)
//  
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://github.com/JeremySkinner/git-dot-aspx

// This file was modified from the one found in git-dot-aspx

#endregion

using System;
using System.Net;
using System.Threading;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.GitServer
{
    public class ReceivePackHandler : GitServerHttpHandler
    {
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IAutoSwapHandler _autoSwapHandler;

        public ReceivePackHandler(ITracer tracer,
                                  IGitServer gitServer,
                                  IOperationLock deploymentLock,
                                  IDeploymentManager deploymentManager,
                                  IRepositoryFactory repositoryFactory,
                                  IAutoSwapHandler autoSwapHandler)
            : base(tracer, gitServer, deploymentLock, deploymentManager)
        {
            _repositoryFactory = repositoryFactory;
            _autoSwapHandler = autoSwapHandler;
        }

        public override void ProcessRequestBase(HttpContextBase context)
        {
            Console.WriteLine("ReceivePackHandler.ProcessRequestBase");
            using (Tracer.Step("RpcService.ReceivePack"))
            {
                // Ensure that the target directory does not have a non-Git repository.
                IRepository repository = _repositoryFactory.GetRepository();
                if (repository != null && repository.RepositoryType != RepositoryType.Git)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    if (context.ApplicationInstance != null)
                    {
                        context.ApplicationInstance.CompleteRequest();
                    }

                    Console.WriteLine("ReceivePackHandler.ProcessRequestBase | Exit 1 | Repo.Type {0}", repository.RepositoryType);
                    return;
                }

                Console.WriteLine("1 ReceivePackHandler.ProcessRequestBase | DeploymentLock.TryLockOperation | IsHeld {0} -- expectin to false", DeploymentLock.IsHeld);
                bool acquired = DeploymentLock.TryLockOperation(() =>
                {
                    Console.WriteLine("2 ReceivePackHandler.ProcessRequestBase | DeploymentLock.TryLockOperation | IsHeld {0} -- expectin to true", DeploymentLock.IsHeld);
                    context.Response.ContentType = "application/x-git-receive-pack-result";

                    if (_autoSwapHandler.IsAutoSwapOngoing())
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        context.Response.Write(Resources.Error_AutoSwapDeploymentOngoing);
                        context.ApplicationInstance.CompleteRequest();

                        Console.WriteLine("ReceivePackHandler.ProcessRequestBase | Exit 2 | AutoSwap Enabled");
                        return;
                    }

                    string username = null;
                    if (AuthUtility.TryExtractBasicAuthUser(context.Request, out username))
                    {
                        GitServer.SetDeployer(username);
                    }

                    Console.WriteLine("ReceivePackHandler.ProcessRequestBase | username {0}", username);
                    UpdateNoCacheForResponse(context.Response);

                    // This temporary deployment is for ui purposes only, it will always be deleted via finally.
                    ChangeSet tempChangeSet;
                    using (DeploymentManager.CreateTemporaryDeployment(Resources.ReceivingChanges, out tempChangeSet))
                    {
                        Console.WriteLine("ReceivePackHandler.ProcessRequestBase | CreateTemporaryDeployment done");
                        GitServer.Receive(context.Request.GetInputStream(), context.Response.OutputStream);
                    }
                    // TODO: Currently we do not support auto-swap for git push due to an issue where we already sent the headers at the
                    // beginning of the deployment and cannot flag at this point to make the auto swap (by sending the proper headers).
                    //_autoSwapHandler.HandleAutoSwap(verifyActiveDeploymentIdChanged: true);
                    Console.WriteLine("3 ReceivePackHandler.ProcessRequestBase | DeploymentLock.TryLockOperation | IsHeld {0} -- expectin to true", DeploymentLock.IsHeld);
                }, TimeSpan.Zero);

                //Console.WriteLine("ReceivePackHandler.ProcessRequestBase | acquired: {0}", acquired);
                Console.WriteLine("4 ReceivePackHandler.ProcessRequestBase | DeploymentLock.TryLockOperation | IsHeld {0} -- expectin to false", DeploymentLock.IsHeld);
                if (!acquired)
                {
                    context.Response.StatusCode = 409;
                    context.ApplicationInstance.CompleteRequest();
                }
            }
        }
    }
}
