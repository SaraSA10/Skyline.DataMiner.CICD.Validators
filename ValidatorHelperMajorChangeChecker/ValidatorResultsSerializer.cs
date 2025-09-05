using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Skyline.DataMiner.CICD.Validators.Common.Interfaces;

namespace ValidatorHelperMajorChangeChecker
{
    public static class ValidatorResultsSerializer
    {
        public static string Serialize(IList<IValidationResult> results)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(IList<IValidationResult>), GetKnownTypes());

            StringBuilder sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb))
            {
                serializer.WriteObject(writer, results);
            }

            string sResults = sb.ToString();
            return sResults;
        }

        public static IList<IValidationResult> Deserialize(string sResults)
        {
            IList<IValidationResult> results;

            DataContractSerializer serializer = new DataContractSerializer(typeof(IList<IValidationResult>), GetKnownTypes());

            using (var reader = XmlReader.Create(new StringReader(sResults)))
            {
                results = (IList<IValidationResult>)serializer.ReadObject(reader);
            }

            return results;
        }

        private static IEnumerable<Type> GetKnownTypes()
        {
            var assembly = typeof(IValidationResult).Assembly;
            var validationResultType = assembly.GetType("Skyline.DataMiner.CICD.Validators.Protocol.Legacy.ValidationResult");

            if (validationResultType != null)
            {
                yield return validationResultType;
            }

            var internalErrorType = assembly.GetType("Skyline.DataMiner.CICD.Validators.Protocol.Common.InternalError");
            if (internalErrorType != null)
            {
                yield return internalErrorType;
            }

            var validationResultTypeCommon = assembly.GetType("Skyline.DataMiner.CICD.Validators.Protocol.Common.ValidationResult");
            if (validationResultTypeCommon != null)
            {
                yield return validationResultTypeCommon;
            }

            var csharpValidationResultType = assembly.GetType("Skyline.DataMiner.CICD.Validators.Protocol.Common.CSharpValidationResult");
            if (csharpValidationResultType != null)
            {
                yield return csharpValidationResultType;
            }
               

        }
    }
}
