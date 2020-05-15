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