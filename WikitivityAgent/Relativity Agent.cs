using kCura.Agent;
using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using Relativity.API;
using System;
using System.Collections.Generic;
using Artifact = kCura.Relativity.Client.DTOs.Artifact;

namespace WikitivityAgent
{
    [kCura.Agent.CustomAttributes.Name("Wikitivity Queue Manager")]
    [System.Runtime.InteropServices.Guid("122b8030-2eb2-4bb8-bf60-d5c8a3725cb5")]
    public class Relativity_Agent : AgentBase
    {
        public static Guid WikitivityRDOGUID = new Guid("17A18C21-3A14-475E-B38D-0837A10DFADE");
        public static string WorkspaceID;
        public static List<int> CasesWithWikitivity = new List<int>();
        public static List<WikiRequest> NewWikiRequestList = new List<WikiRequest>();
        public static int? AdminObjectInfo;
        public class WikiRequest
        {
            public string WorkspaceID { get; set; }
            public string RequestURL { get; set; }
            public string Status { get; set; }

        }


        public override void Execute()

        {

            RaiseMessage("Entering operations",1);
            IAPILog logger = Helper.GetLoggerFactory().GetLogger();
            RaiseMessage("Log generated", 1);

            try
            {
                #region Obtain a full list of Case IDs where Wikitivity is installed (CasesWithWikitivity)
                using (IRSAPIClient proxy = Helper.GetServicesManager().CreateProxy<IRSAPIClient>(ExecutionIdentity.CurrentUser))
                {
                    RaiseMessage("Obtaining Case IDs", 1);


                    Query<Workspace> getCaseIDs = new Query<Workspace>();
                    getCaseIDs.Condition = new WholeNumberCondition("Artifact ID", NumericConditionEnum.IsSet);
                    getCaseIDs.Fields = FieldValue.AllFields;
                    QueryResultSet<Workspace> CaseQueryResults = new QueryResultSet<Workspace>();
                    proxy.APIOptions.WorkspaceID = -1;
                    try
                    {
                        CaseQueryResults = proxy.Repositories.Workspace.Query(getCaseIDs, 0);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "There was an exception.");
                        RaiseMessage("Obtaining Case IDIZZLESs " + ex.ToString() + "\r\n " + ex.InnerException + " \r\n" + ex.StackTrace + "\r\n" + ex.Message, 1);
                        throw; ;
                    }
                    if (CaseQueryResults.Success)
                    {
                        RaiseMessage("Obtained Case IDs", 1);
                    }

                    RaiseMessage("Obtaining Case IDs with Wikitivity installed", 1);
                    foreach (var caseID in CaseQueryResults.Results)
                    {
                        var singleWorkSpace = caseID.Artifact.ArtifactID;

                        proxy.APIOptions.WorkspaceID = singleWorkSpace;

                        Query<ObjectType> checkForWikitivity = new Query<ObjectType>();
                        //checkForWikitivity.ArtifactTypeGuid = WikitivityRDOGUID;
                        checkForWikitivity.Condition = new TextCondition("Name", TextConditionEnum.EqualTo, "Wikitivity Request");
                        checkForWikitivity.Fields = FieldValue.AllFields;

                        QueryResultSet<ObjectType> QueryForWikitivity = new QueryResultSet<ObjectType>();
                        try
                        {
                            QueryForWikitivity = proxy.Repositories.ObjectType.Query(checkForWikitivity, 0);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "There was an exception.");
                            RaiseMessage("Getting Case IDs with Wikitivity installed " + e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                            throw;
                        }
                        if (QueryForWikitivity.TotalCount > 0)
                        {
                            CasesWithWikitivity.Add(singleWorkSpace);

                        }

                    }
                    RaiseMessage("Obtained Case IDs with Wikitivity installed", 1);
                    #endregion
                    foreach (var workspace in CasesWithWikitivity)
                    {//adding each case to the queue!
                        proxy.APIOptions.WorkspaceID = workspace;


                        //I need to query for the status here!
                        Query<RDO> WikiRequestRDOQuery = new Query<RDO>();
                        WikiRequestRDOQuery.Condition = new TextCondition("Status", TextConditionEnum.IsSet);
                        WikiRequestRDOQuery.Fields = FieldValue.AllFields;
                        WikiRequestRDOQuery.ArtifactTypeGuid = WikitivityRDOGUID;
                        //Do I need to add a fieldvalue here?

                        try
                        {
                            QueryResultSet<RDO> wikiquestStatusCheck = //new QueryResultSet<RDO>();

                                proxy.Repositories.RDO.Query(WikiRequestRDOQuery);
                            foreach (var wikiStatReq in wikiquestStatusCheck.Results)
                            {
                                var wikiReqReq = wikiStatReq.Artifact; //I'm so sorry I'm out of names for these
                                var wikiReqReqStatus = wikiReqReq.Fields.Get("Status").ToString();

                                #region if status is 0
                                if (wikiReqReqStatus == "0")
                                {
                                    //COPY PASTA ALL THE LOGIC INTO HERE FOR CREATION OF NEW ROW

                                    NewWikiRequestList.Add(new WikiRequest
                                    {
                                        RequestURL = wikiReqReq.Fields.Get("RequestURL").ToString(),
                                        Status = wikiReqReq.Fields.Get("Status").ToString(),
                                        WorkspaceID = wikiReqReq.Fields.Get("WorkspaceID").ToString()
                                    });
                                    //entry added now I need to update 


                                    /* I ALSO WANT TO WRITE THIS SOMEWHERE BEFORE I guess?
                                     * 
                                     * 
                                     * We will be writing it to the wikitivity master queue at the admin level.
                                     * 
                                     * This will require we create a post-install event handler that actually creates the admin level object and fields since we can't add it to a RAP.
                                     * */


                                    





                                    List<Guid> WikitivityRDOGuidStatusUpdate = new List<Guid>();
                                    WikitivityRDOGuidStatusUpdate.Add(WikitivityRDOGUID);
                                 RDO WikiRequestRDOStatusUpdate = new RDO(wikiReqReq.ArtifactID);
                                    WikiRequestRDOStatusUpdate.ArtifactTypeGuids = WikitivityRDOGuidStatusUpdate;
                                    WikiRequestRDOStatusUpdate.Fields.Add(new FieldValue()
                                    {
                                        Name = "Status",
                                        Value = "1"
                                
                                    });
                                    //WikiRequestRDOStatusUpdate.ArtifactID = 
                                    try
                                    {
                                        WriteResultSet<RDO> updateTheRequestResultSet =
                                        proxy.Repositories.RDO.Update(WikiRequestRDOStatusUpdate);

                                        if (updateTheRequestResultSet.Success)
                                        {
                                            RaiseMessage("Updated a request in workspace " + workspace, 1);
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        logger.LogError(e, "There was an exception.");
                                       RaiseMessage("Updating the Request in the Workspace " + e.ToString() + "\r\n " + e.InnerException +" \r\n"+ e.StackTrace + "\r\n" + e.Message, 1);
                                        throw;
                                    }

                                };
                                #endregion

                                if (wikiReqReqStatus == "2")
                                {
                                    
                                    //Delete the record we done  here
                                    try
                                    {
                                        proxy.Repositories.RDO.Delete(wikiReqReq.ArtifactID);

                                        RaiseMessage("Wikitivity Request " + wikiReqReq.ArtifactID + " is complete. Request deleted.", 1);
                                    }
                                    catch (Exception e)
                                    {
                                        logger.LogError(e, "There was an exception.");
                                        RaiseMessage("Deleting the workspace request " + e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                                        throw;
                                    }

                                }
                            }

                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "There was an exception.");
                            RaiseMessage("Obtaining Status values " + e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                            throw;
                        }
                        //IDBContext dbContext = this.Helper.GetDBContext(workspace);
                        //if (CheckForRows(dbContext).Equals(true))
                        //{
                        //    //this means there is a new entry in the DB in a case. We can make a 
                        //    //master list? 
                        //    //perform a SQL read on that row!
                        //    WikiRequest newWikiRequest;

                        //    string sqlGetNewWikitivityRequest = @"SELECT [WorkspaceID],
                        //                                        [RequestURL],[Status]
                        //                                        FROM [EDDS" + workspace+"]" +
                        //                                        ".[EDDSDBO].[WikitivityRequest]";
                        //    var reader = dbContext.ExecuteSQLStatementAsReader(sqlGetNewWikitivityRequest);
                        //    while (reader.Read())

                        //        NewWikiRequestList.Add(new WikiRequest
                        //        {
                        //            RequestURL = reader.GetString(1),
                        //            Status = reader.GetString(2),
                        //            WorkspaceID = reader.GetString(0)
                        //        });
                        //    reader.Close();

                        //    //delete the contents of the table AFTER we write to main storage
                        //}

                    }

                    proxy.APIOptions.WorkspaceID = -1;
                    Query<ObjectType> getMasterQueueInfo = new Query<ObjectType>();
                    getMasterQueueInfo.Condition = new TextCondition("Name", TextConditionEnum.EqualTo, "Wikitivity Queue");
                    getMasterQueueInfo.Fields = kCura.Relativity.Client.DTOs.FieldValue.AllFields;

                    QueryResultSet<ObjectType> masterQueueQueryResultSet = new QueryResultSet<ObjectType>();

                    try
                    {
                        masterQueueQueryResultSet = proxy.Repositories.ObjectType.Query(getMasterQueueInfo, 0);


                        foreach (var item in masterQueueQueryResultSet.Results)
                        {
                            var oneMore = item.Artifact;

                            AdminObjectInfo = oneMore.DescriptorArtifactTypeID;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "There was an exception.");
                        RaiseMessage("Obtaining Master Queue Info " + e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                        throw;
                    }


                    foreach (var updateToMasterList in NewWikiRequestList)
                    {
                        proxy.APIOptions.WorkspaceID = -1;

                        RDO updateMasterQueueRDO = new RDO();
                        updateMasterQueueRDO.ArtifactTypeID = AdminObjectInfo;

                        //updateMasterQueueRDO.ArtifactTypeName = "Wikitivity Request";
                        updateMasterQueueRDO.Fields.Add(new FieldValue()
                        {
                            Name = "RequestURL",
                            Value = updateToMasterList.RequestURL
                        });6
                        updateMasterQueueRDO.Fields.Add(new FieldValue()
                        {
                            Name = "WorkspaceID",
                            Value = updateToMasterList.WorkspaceID
                        });
                        updateMasterQueueRDO.Fields.Add(new FieldValue()
                        {
                            Name = "Status",
                            Value = "1"
                        });

                        try
                        {
                            var createNewMasterQueueEntry = proxy.Repositories.RDO.Create(updateMasterQueueRDO);
                            if (createNewMasterQueueEntry.Success)
                            {
                                RaiseMessage("Created new Master queue entry " + createNewMasterQueueEntry.Message, 1);

                            }
                            if (!createNewMasterQueueEntry.Success)
                            {
                                RaiseMessage("Failed to Create new Master queue entry " + updateMasterQueueRDO.ArtifactID, 1);

                            }
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "There was an exception.");
                            RaiseMessage("Creating new master queue entry " +  e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                            throw;
                        }
                    }
                    NewWikiRequestList.Clear();


                }

                //should I update the MasterQueue list here?



                //From here we want to query each case with wiki installed to see if there are rows in the table. If there are 
                // it should execute the call, pull back the data and create new files then upload them to the appropriate workspace.
                logger.LogVerbose("Log information throughout execution.");
            }
            catch (Exception e)
            {
                //Your Agent caught an exception
                logger.LogError(e, "There was an exception.");
                RaiseMessage("The whole friggin thing just borked " + e.ToString() + "\r\n " + e.InnerException + " \r\n" + e.StackTrace + "\r\n" + e.Message, 1);
                throw;
            }
        }

        
        /// <summary>
        /// Returns the name of agent
        /// </summary>
        public override string Name
        {
            get
            {
                return "Agent Name";
            }
        }
    }
}