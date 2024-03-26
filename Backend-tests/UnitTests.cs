using flare_csharp;
using System.Text.RegularExpressions;
using Moq;
using AutoFixture;
using NLog;
using Flare;

namespace Backend_tests
{
    
    public class UnitTests
    {
        
    
        [Theory]
        [InlineData("GalaÙbongas®78")]
        [InlineData("HobÜoblinas§45")]
        [InlineData("LowBobÔas42©")]
        [InlineData("HoÕbGoblinas42©")]
        [InlineData("Gobl£ingoÔnas42£")]
        public async Task Test_RegisterToServer_Username_NotValid_ContainsNotASCIIChar(string username2)
        {
            //Arrange
            Client.Username = username2;
            //Act & Assert
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
            if(!(ex.Message != null))
            {
                Assert.Fail();
            }

        }
        [Theory]
        [InlineData("HobÜoblinas§45", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("Gobl£ingoÔnas42£", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("HoÕbGoblinas42©", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("LowBobÔas42©", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("GalaÙbongas®78£", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        public async Task Registration_prevented_given_username_containing_non_ASCII_characters(string username4, string password)
        {
            Client.Username = username4;
            Client.Password = password;
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);
            if (!(ex.Message != null))
            {
                Assert.Fail();
            }
        }
        [Theory]
        [InlineData("LowBobas42sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("LowBobas4444444444444444444444444444444444444444444", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("ghasjgasjkgbahksgbaskgbaskgbasjgkbasgasgasgas", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("546484987984684987498746454165489", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("DidelisMedisKeturiPenkiAstuoniDesimt15!", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]

        public async Task Registration_prevented_given_username_with_out_of_bounds_length(string username5, string password1)
        {
            Client.Username = username5;
            Client.Password = password1;
            var ex = await Assert.ThrowsAsync<ClientOperationFailedException>(Client.RegisterToServer);          
            if (!(ex.Message != null))
            {
                Assert.Fail();
            }
        }
        [Theory]
        [InlineData("LowBobas4546", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("BaltasL%%%??@4", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("AsciiNinja123$", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("User1234!ASCII", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        [InlineData("herkus_leon_kaselis_3", "n:+l@/~t}E:~\\7:N}\"ELR.8<9")]
        public async Task Registration_allowed_given_compliant_username(string username6, string password2)
        {
            Client.Username = username6;
            Client.Password = password2;
            await Client.ConnectToServer();
            try
            {
                await Client.RegisterToServer();
            }
            catch (Exception ex)
            {
                
                Assert.Fail(ex.Message);
            }
        }
        [Theory]
        [InlineData("", "")]
        public async Task Registration_prevented_given_blank_password(string username7, string password3)
        {
            Client.Username = username7;
            Client.Password = password3;
        }
        [Theory]
        [InlineData("", "")]
        public async Task Registration_prevented_given_weak_password(string username8, string password4)
        {
            Client.Username = username8;
            Client.Password = password4;
        }
        [Theory]
        [InlineData("", "")]
        public async Task Registration_prevented_given_too_long_password(string username9, string password5)
        {
            Client.Username = username9;
            Client.Password = password5;
        }
        [Theory]
        [InlineData("", "")]
        public async Task Registration_allowed_given_ok_password(string username10, string password6)
        {
            Client.Username = username10;
            Client.Password = password6;
        }
        [Theory]
        [InlineData("", "")]
        public async Task Registration_allowed_given_strong_password(string username11, string password7)
        {
            Client.Username = username11;
            Client.Password = password7;
        }

    }
}