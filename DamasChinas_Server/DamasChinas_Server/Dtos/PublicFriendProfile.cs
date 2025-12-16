using System.Runtime.Serialization;

namespace DamasChinas_Server.Dtos
{
    [DataContract]
    public class PublicFriendProfile
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public string SocialUrl { get; set; }

        [DataMember]
        public string AvatarFile { get; set; }

        [DataMember]
        public int MatchesPlayed { get; set; }

        [DataMember]
        public int Wins { get; set; }

        [DataMember]
        public int Loses { get; set; }
    }
}
