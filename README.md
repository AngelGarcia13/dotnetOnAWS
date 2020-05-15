# dotnet On AWS

Notion Doc: https://www.notion.so/dotnet-On-AWS-4b1915d9e755469290ef67e5c0dd2d4d

# First things first.

### Create an AWS account.

To create an AWS account, see [How do I create and activate a new Amazon Web Services account?](https://aws.amazon.com/premiumsupport/knowledge-center/create-and-activate-aws-account)

### Create AWS credentials.

To perform these tutorials, you need to create an IAM (Identity and Access Management) user and obtain credentials for that user. Once you have those credentials, you make them available to the SDK in your development environment. Here's how:

- Sign in to the AWS Management Console and open the IAM console at [https://console.aws.amazon.com/iam/](https://console.aws.amazon.com/iam/)
- Create an user with programmatic access and name it "dotnet-tutorials-user", then add the permission (using an existing policy) for use the S3 service (AmazonS3FullAccess).

### Configure credentials in your local environment.

- Create the shared AWS credentials file. This file is ~/.aws/credentials on Linux and macOS systems and %HOME%\.aws\credentials on Windows.
- Add the following to the file:

```
[dotnet-tutorials-user]

aws_access_key_id = YOUR_ACCESS_KEY_ID

aws_secret_access_key = YOUR_SECRET_ACCESS_KEY
```

- Add necessary temporary env variables for the SDK configuration values:

```
export AWS_PROFILE='dotnet-tutorials-user'
export AWS_REGION='us-east-1'
```

## Demo 1 - AWS SDK for .NET Apps.

Let's create a console app and add the AWS SDK package to interact with a Simple Storage Service (S3) for create a bucket and upload/list files in the bucket.

```
dotnet new console --name S3CreateAndList
```

```
cd S3CreateAndList
```

```
dotnet add package AWSSDK.S3
```

Add the following code to your Program class:

```csharp
using System;
using Amazon.S3;

namespace S3CreateAndList
{
    class Program
    {
        /*
        Before running ensure that you have your access key and secret set in ~/.aws/credentials
        */
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            #region Part 1 - Use the S3 client and create a bucket if doesn't exist

            const string bucketName = "angelrenegarcia-s3-test-bucket-2"; //Unique per region (globally)
            //Create an S3 client object.
            var client = new AmazonS3Client();

            //Creating the bucket
            Console.WriteLine($"Creating bucket {bucketName} if doesn't exist...");
            var response = await client.PutBucketAsync(bucketName);
            Console.WriteLine($"Result: {response.HttpStatusCode.ToString()}");

            #endregion

            #region Part 2 - Upload files to the bucket
            
            //Uploading files to the bucket
            Random random = new Random();
            await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest()
            {
                Key = $"SampleFile{DateTime.Now.ToString("mm-dd-yyyy-HH-MM-ss")}{random.Next(1, 1000)}",
                ContentBody = "Sample file body...",
                ContentType = "text/plain",
                BucketName = bucketName
            });
            
            #endregion
            
            #region Part 3 - List all files in the bucket

            //List all files in the bucket
            var files = await client.ListObjectsAsync(bucketName);
            foreach (var item in files.S3Objects)
            {
                Console.WriteLine($"{item.Key} - {item.Size}");
            }    
            
            #endregion
            

        }
    }
}
```

## Demo 2 - AWS Extensions for .NET CLI.

Let's create and deploy an AWS Lambda function through the CLI using a template (Lambda Simple S3 Function) with an event listener pointing to our previously created S3 bucket, so when a new file is uploaded the function will send me sms with the help of the Simple Notification Service (SNS).

```
dotnet tool install -g Amazon.Lambda.Tools
```

```
dotnet new --help
```

```
dotnet new lambda.S3 --name SimpleS3LambdaFunction
```

```
cd SimpleS3LambdaFunction/src/SimpleS3LambdaFunction
```

Add a LogLine before return the *return response.Headers.ContentType;*

```json
string message = $"{s3Event.Object.Key} - {s3Event.Object.Size} Bytes";
context.Logger.LogLine(message);
```

Add your profile and region values to the aws-lambda-tools-defaults.json

```json
"profile": "dotnet-tutorials-user",
"region": "us-east-1"
```

Now we can deploy our function to AWS

```
dotnet lambda deploy-function
```

Or

```
dotnet lambda deploy-function --region us-east-1 --profile dotnet-tutorials-user
```

Go to your function configuration in AWS to **set a trigger**, choose S3, select your previously created bucket and select "all object create events" for the Event type.

To see the function in action just upload a file to your bucket.

Ok, let's add the Simple Notification Service to our lambda function so we can send a SMS to our phone number:

```json
dotnet add package AWSSDK.SimpleNotificationService
```

Modify your function code like this:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleS3LambdaFunction
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }
        IAmazonSimpleNotificationService SnsClient { get; set; }
        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
            SnsClient = new AmazonSimpleNotificationServiceClient();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 and SNS client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="snsClient"></param>
        public Function(IAmazonS3 s3Client, IAmazonSimpleNotificationService snsClient)
        {
            this.S3Client = s3Client;
            this.SnsClient = snsClient;
        }
        
        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            if(s3Event == null)
            {
                return null;
            }

            try
            {
                var response = await S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                string message = $"{s3Event.Object.Key} - {s3Event.Object.Size} Bytes";
                context.Logger.LogLine(message);
                PublishRequest request = new PublishRequest
                {
                    Message = message,
                    PhoneNumber = "+34642375554"
                };

                var smsResponse = await SnsClient.PublishAsync(request);
                context.Logger.LogLine($"Response from SNS: {smsResponse.HttpStatusCode}");
                return response.Headers.ContentType;
            }
            catch(Exception e)
            {
                context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }
        }
    }
}
```

Re-deploy the lambda (use the same name or will create a new one)

```
dotnet lambda deploy-function
```

Now upload a file to the bucket, this will trigger the function and a SMS will be sent to the number that we put in the code.

## Demo 3 - AWS Toolkit for Visual Studio.

Let's explore the toolkit in Visual Studio, create a simple ASP.NET Core API and deploy it to an AWS Elastic Beanstalk.

## Demo 4 - AWS Toolkit for VS Code.

Let's see how we can invoke an AWS Lambda function using the VS Code toolkit.

## Demo 5 - AWS Toolkit for Azure DevOps.

Let's see how to add an AWS task to our Azure DevOps pipelines.
