using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ.Build
{
    public enum BuildEnvironmentEnum
    { 
        Develop,
        QA,
        Stage,
        Product
    }

    public class BuildEnvironmentData
    {
        public BuildEnvironmentEnum BuildEnvironment = BuildEnvironmentEnum.Develop;

        public string ServerURL = string.Empty;
        public string ServerPort = string.Empty;

        public string GameChat_Project_ID = string.Empty;

        public string GameChat_ChatChannel = string.Empty;
        public string GameChat_SystemChannel = string.Empty;
        public string GameChat_NoticeChannel = string.Empty;
        public string GameChat_AdminChannel = string.Empty;
    }

    [CreateAssetMenu(fileName = "BuildEnvironmentSelect", menuName = "Table/BuildEnvironmentSelect", order = 1)]
    public class BuildEnvironmentSelectAsset : ScriptableObject
    {
        public BuildEnvironmentEnum BuildElement = BuildEnvironmentEnum.Develop;

        private Dictionary<BuildEnvironmentEnum, BuildEnvironmentData> m_buildElementData = new Dictionary<BuildEnvironmentEnum, BuildEnvironmentData>();

        public void OnEnable()
        {
            m_buildElementData.Clear();

            {
                // Dev
                BuildEnvironmentData buildEnvironmentData = new BuildEnvironmentData();

                buildEnvironmentData.ServerURL = "http://110.165.18.148:{0}/";
                buildEnvironmentData.ServerPort = "3013";

                buildEnvironmentData.GameChat_Project_ID = "599da301-d7a7-47c6-aa90-bfe4f46bf413";

                buildEnvironmentData.GameChat_ChatChannel = "6af8bbe3-9fa7-4a0e-add9-8b1b781c8bf9";
                buildEnvironmentData.GameChat_SystemChannel = "6c8c6e3f-1a84-409c-be22-25b6e29128ad";
                buildEnvironmentData.GameChat_NoticeChannel = "4cf17988-884f-4a37-87e2-50e76d34f64b";
                buildEnvironmentData.GameChat_AdminChannel = "1d384ed2-f865-467c-bf3a-fddb91ae21de";

                m_buildElementData.Add(BuildEnvironmentEnum.Develop, buildEnvironmentData);
            }

            {
                // QA
                BuildEnvironmentData buildEnvironmentData = new BuildEnvironmentData();

                buildEnvironmentData.ServerURL = "http://223.130.163.194:{0}/";
                buildEnvironmentData.ServerPort = "9001";

                buildEnvironmentData.GameChat_Project_ID = "886e5bef-633e-4d64-8312-fd51be1794b7";

                buildEnvironmentData.GameChat_ChatChannel = "0656226f-3097-4654-9cd3-f6e65297a176";
                buildEnvironmentData.GameChat_SystemChannel = "d4d54ac8-2934-4cd6-ab4a-c0b4cf388893";
                buildEnvironmentData.GameChat_NoticeChannel = "98f7a2b4-7645-4842-b115-92a2466422d1";
                buildEnvironmentData.GameChat_AdminChannel = "87ce38ed-3e08-4132-baca-c6fea152fa68";

                m_buildElementData.Add(BuildEnvironmentEnum.QA, buildEnvironmentData);
            }

            {
                // Stage
                BuildEnvironmentData buildEnvironmentData = new BuildEnvironmentData();

                buildEnvironmentData.ServerURL = "https://y6pfbidkqm.apigw.ntruss.com/studio-dk/staging/";
                buildEnvironmentData.ServerPort = string.Empty;

                buildEnvironmentData.GameChat_Project_ID = "886e5bef-633e-4d64-8312-fd51be1794b7";

                buildEnvironmentData.GameChat_ChatChannel = "0656226f-3097-4654-9cd3-f6e65297a176";
                buildEnvironmentData.GameChat_SystemChannel = "d4d54ac8-2934-4cd6-ab4a-c0b4cf388893";
                buildEnvironmentData.GameChat_NoticeChannel = "98f7a2b4-7645-4842-b115-92a2466422d1";
                buildEnvironmentData.GameChat_AdminChannel = "87ce38ed-3e08-4132-baca-c6fea152fa68";

                m_buildElementData.Add(BuildEnvironmentEnum.Stage, buildEnvironmentData);
            }

            {
                // Product
                BuildEnvironmentData buildEnvironmentData = new BuildEnvironmentData();

                buildEnvironmentData.ServerURL = "https://y6pfbidkqm.apigw.ntruss.com/studio-dk/production/";
                buildEnvironmentData.ServerPort = string.Empty;

                buildEnvironmentData.GameChat_Project_ID = "d1b4b045-9787-4dc4-95f6-7bbaa25f2e86";

                buildEnvironmentData.GameChat_ChatChannel = "e32f3618-76ce-4699-9bc1-286eb1a5eff6";
                buildEnvironmentData.GameChat_SystemChannel = "ebf5375d-811f-4d3b-8e8e-afc02fc03605";
                buildEnvironmentData.GameChat_NoticeChannel = "5eb0b6cd-59a1-4ba2-8f75-c4cdf567f46e";
                buildEnvironmentData.GameChat_AdminChannel = "98d419e0-3592-493a-9be6-3bf609205c95";

                m_buildElementData.Add(BuildEnvironmentEnum.Product, buildEnvironmentData);
            }
        }
        //------------------------------------------------------------------------------------
        public BuildEnvironmentData GetEnvironmentData()
        {
            return m_buildElementData[BuildElement];
        }
        //------------------------------------------------------------------------------------
    }
}
