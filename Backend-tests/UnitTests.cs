
using Flare;
using flare_csharp;
using System.Text.RegularExpressions;
using static flare_csharp.Client;


namespace Backend_tests
{
    
    public class UnitTests
    {


        [Theory]
        [InlineData("Low Bobikas as42")]
        [InlineData("                ")]
        [InlineData("LowXXXX Bobas42  ©")] 
        [InlineData("LowBo bas425464654654  654654654  ")]
        [InlineData("Low  Bo  bas   42")]
        public async Task Test_RegisterToServer_Username_NotValid_HasSpaces(string username)
        {

            //Arrange
            Client.Username = username;      
            //Act & Assert
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
            Assert.Equal("Client username: " + Client.Username + " is not valid", ex.Message);
            
        }
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async void Test_RegisterToServer_Username_NotValid_IsEmptyOrNull(string username1)
        {
            //Arrange          
            Client.Username = username1;
            //Act & Assert
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
            Assert.Equal("Client username: " + Client.Username + " is not valid", ex.Message);
            
        }
        [Theory]
        [InlineData("GalaŸbongasÆ78")]
        [InlineData("Hob‹oblinasß45")]
        [InlineData("LowBob‘as42©")]
        [InlineData("Ho’bGoblinas42©")]
        [InlineData("Gobl£ingo‘nas42£")]
        public async void Test_RegisterToServer_Username_NotValid_ContainsNotASCIIChar(string username2)
        {
            //Arrange
            Client.Username = username2;
            //Act & Assert
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
            Assert.Equal("Client username: " + Client.Username + " is not valid", ex.Message);
            
        }
        [Theory]
        [InlineData("")]
        [InlineData(" MonkaminiDeluxe")]
        [InlineData("LowBobas4@@@@@@@@@@@@@@2©")]
        [InlineData("LowBobas4444444444444444444444444444444444444444444")]
        [InlineData("LowBobas42sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
        public async void Test_RegisterToServer_Username_NotValid_DoesntMatchRegex(string username3)
        {

            //Arrange
            Regex regex = new Regex(@"^[\d\w]{1,32}$", RegexOptions.IgnoreCase);
            Client.Username = username3;
            if (!regex.IsMatch(Client.Username))
            {
                var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
                Assert.Equal("Client username: " + Client.Username + " is not valid", ex.Message);
            }
            else
                Assert.Fail("Username mathches the regex");

           

        }


    }
}