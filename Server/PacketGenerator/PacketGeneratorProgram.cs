using System;
using System.IO;
using System.Xml;

namespace PacketGenerator
{
    class PacketGeneratorProgram
    {
        // 실시간으로 parsing 하는 데이터들을 보관
        static string genPacket;

        static void Main(string[] args)
        {
            // xml parsing
            XmlReaderSettings settings = new XmlReaderSettings()
            {
                // 주석 무시
                IgnoreComments = true,
                // 스페이스 무시
                IgnoreWhitespace = true
            };
            using (XmlReader r = XmlReader.Create("PDL.xml", settings))
            {
                // 바로 본문으로 이동
                // <?xml version="1.0" encoding="utf-8" ?> 건너뜀
                r.MoveToContent();

                // xml을 한줄 씩 읽음
                while (r.Read())
                {
                    // r.Depth == 1 : 바로 xml 본문으로 이동 => <packet name="PlayerInfoReq">으로 이동
                    // r.NodeType == XmlNodeType.Element : packet이 현재 내부 요소 일 때
                    if (r.Depth == 1 && r.NodeType == XmlNodeType.Element)
                    {
                        ParsePacket(r);
                    }
                    // r.Name : 타입
                    // r["name"] : 변수명
                    // System.Console.WriteLine(r.Name + " " + r["name"]);
                }
                File.WriteAllText("GenPacket.cs", genPacket);
            }
            ;
        }

        public static void ParsePacket(XmlReader r)
        {
            if (r.NodeType == XmlNodeType.EndElement)
                return;
            if (r.Name.ToLower() != "packet")
            {
                System.Console.WriteLine("Invalid packet name");
                return;
            }
            string packetName = r["name"];
            if (string.IsNullOrEmpty(packetName))
            {
                System.Console.WriteLine("Packet without packet");
                return;
            }
            // Tuple은 3개 이상의 값을 반환할 수 있는 자료형
            Tuple<string, string, string> t = ParseMembers(r);
            genPacket += string.Format(PacketFormat.packetFormat, packetName, t.Item1, t.Item2, t.Item3);
        }

        // {1} 멤버 변수들
        // {2} 멤버 변수의 Read
        // {3} 멤버 변수의 Write
        public static Tuple<string, string, string> ParseMembers(XmlReader r)
        {
            string packetName = r["name"];

            string memberCode = "";
            string readCode = "";
            string writeCode = "";

            // parsing 대상 데이터
            int depth = r.Depth + 1;
            while (r.Read())
            {
                // 현재 depth가 내가 원하는 depth가 아니라면 빠져나가기
                if (r.Depth != depth)
                    break;
                string memberName = r["name"];
                if (string.IsNullOrEmpty(memberName))
                {
                    System.Console.WriteLine("Member without name");
                    return null;
                }

                // memberCode에 이미 내용물이 있다면
                // xml 파싱할 때 한칸 띄어쓰기 해줌
                if (string.IsNullOrEmpty(memberCode) == false)
                    memberCode += Environment.NewLine;
                if (string.IsNullOrEmpty(readCode) == false)
                    readCode += Environment.NewLine;
                if (string.IsNullOrEmpty(writeCode) == false)
                    writeCode += Environment.NewLine;

                // 멤버 타입
                string memberType = r.Name.ToLower();
                switch (memberType)
                {
                    case "bool":
                    case "short":
                    case "ushort":
                    case "int":
                    case "long":
                    case "float":
                    case "double":
                        // 고정된 사이트의 타입이라 여기서 한번 끊어줌
                        // xml에서 memberFormat, readFormat, writeFormat으로 묶어줄 수 있음
                        memberCode += string.Format(PacketFormat.memberFormat, memberType, memberName);
                        readCode += string.Format(PacketFormat.readFormat, memberName, ToMemberType(memberType), memberType);
                        writeCode += string.Format(PacketFormat.writeFormat, memberName, memberType);
                        break;
                    case "string":
                        memberCode += string.Format(PacketFormat.memberFormat, memberType, memberName);
                        readCode += string.Format(PacketFormat.readStringFormat, memberName, memberName);
                        writeCode += string.Format(PacketFormat.writeStringFormat, memberName);
                        break;
                    case "list":
                        Tuple<string, string, string> t = ParseList(r);
                        memberCode += t.Item1;
                        readCode += t.Item2;
                        writeCode += t.Item3;
                        break;
                    default:
                        break;

                }
            }
            // 한 칸 띄어쓰기가 된 다음에 tap으로 교체
            memberCode = memberCode.Replace("\n", "\n\t");
            readCode = readCode.Replace("\n", "\n\t\t");
            writeCode = writeCode.Replace("\n", "\n\t\t");
            return new Tuple<string, string, string>(memberCode, readCode, writeCode);
        }

        private static Tuple<string, string, string> ParseList(XmlReader r)
        {
            string listName = r["name"];
            if (string.IsNullOrEmpty(listName))
            {
                System.Console.WriteLine("List without name");
                return null;
            }

            Tuple<string, string, string> t = ParseMembers(r);
            string memberCode = string.Format(PacketFormat.memberListFormat,
                FirstCharToUpper(listName),
                FirstCharToLower(listName),
                t.Item1,
                t.Item2,
                t.Item3);

            string readCode = string.Format(PacketFormat.readListFormat,
                FirstCharToUpper(listName),
                FirstCharToLower(listName)
            );

            string writeCode = string.Format(PacketFormat.writeListFormat,
                FirstCharToUpper(listName),
                FirstCharToLower(listName)
            );
            return new Tuple<string, string, string>(memberCode, readCode, writeCode);
        }

        public static string ToMemberType(string memberType)
        {
            switch (memberType)
            {
                case "bool":
                    return "ToBoolean";
                // byte 배열을 파싱하는 것이기 때문에 byte는 건너뛰어야함
                // case "byte":
                case "short":
                    return "ToInt16";
                case "ushort":
                    return "ToUInt16";
                case "int":
                    return "ToInt32";
                case "long":
                    return "ToInt64";
                case "float":
                    return "ToSingle";
                case "double":
                    return "ToDouble";
                default:
                    return "";
            }
        }
        public static string FirstCharToUpper(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }
            // 첫 번째 문자를 대문자로 바꾼 다음 기존에 있던 소문자 제거
            return input[0].ToString().ToUpper() + input.Substring(1);
        }
        public static string FirstCharToLower(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }
            // 첫 번째 문자를 대문자로 바꾼 다음 기존에 있던 소문자 제거
            return input[0].ToString().ToLower() + input.Substring(1);
        }
    }
}