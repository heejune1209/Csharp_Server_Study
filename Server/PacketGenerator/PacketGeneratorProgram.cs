using System;
using System.IO;
using System.Xml;

namespace PacketGenerator
{
    // PacketGeneratorProgram 클래스의 주요 역할
    // 1. XML 파일 읽기 및 파싱:
    // PDL.xml 파일 내에 정의된 패킷 및 멤버(필드) 정보
    // (예: Packet 이름, 데이터 타입, 멤버 이름 등)를 읽고 파싱합니다.
    
    // 2. 코드 생성:
    // 파싱된 정보를 PacketFormat.cs에 정의된 템플릿 문자열에 삽입하여,
    // 최종적으로 C# 클래스 코드(예: 각 패킷 클래스)를 생성합니다.
    
    // 3. 파일 출력:
    // 생성된 코드를 "GenPacket.cs" 파일에 기록하여,
    // 나중에 프로젝트에 포함하거나 사용할 수 있도록 합니다.
    class PacketGeneratorProgram
    {
        // 실시간으로 parsing 하는 데이터들을 보관
        static string genPacket;
        static ushort packetId;
        static string packetEnum;

        static void Main(string[] args)
        {

            // pdl 경로
            string pdlPath = "../PDL.xml";

            // XML 파싱을 위한 설정 생성
            XmlReaderSettings settings = new XmlReaderSettings()
            {
                // 주석 무시
                IgnoreComments = true,
                // 스페이스 무시
                IgnoreWhitespace = true
            };

            // 여기다가 Main 즉 처음에 프로그램을 실행할 때 인자를 받아 가지고
            // 여기 있는 경로를 바꿔줄 수 있게 옵션으로 넣어 주도록 하겠습니다
            // 즉, 프로그램이 실행될 때 인자를 넣어주면 인자로 뭔가를 넘겨줬다고 하면 
            // 이런식으로 파싱을 해가지고 해가지고 pdl 패스에다가 넣어준 다음에
            // 개를 이용해서 기존에 했던 create를 이어간다
            if (args.Length >= 1)
                pdlPath = args[0];

            using (XmlReader r = XmlReader.Create(pdlPath, settings))
            {
                // 바로 본문으로 이동
                // <?xml version="1.0" encoding="utf-8" ?> 건너뜀
                r.MoveToContent();

                // xml을 한줄 씩 읽음
                while (r.Read())
                {
                    // 깊이(depth)가 1이고, 현재 노드가 요소(Element)인 경우
                    // -> 패킷 정보가 있는 엘리먼트(예: <packet name="PlayerInfoReq">)를 의미
                    // r.Depth == 1 : 바로 xml 본문으로 이동 => <packet name="PlayerInfoReq">으로 이동
                    // r.NodeType == XmlNodeType.Element : packet이 현재 내부 요소 일 때
                    if (r.Depth == 1 && r.NodeType == XmlNodeType.Element)
                    {
                        ParsePacket(r);
                    }
                    // 필요에 따라, r.Name과 r["name"]을 출력할 수 있음 (디버깅용)
                    // r.Name : 타입
                    // r["name"] : 변수명
                    // System.Console.WriteLine(r.Name + " " + r["name"]);
                }
                string fileText = string.Format(PacketFormat.fileFormat, packetEnum, genPacket);
                File.WriteAllText("GenPackets.cs", fileText);
            };
        }
        // 패킷 검증:
        // r.NodeType이 EndElement이면 반환하고, 노드 이름이 "packet"이 아니면 오류 메시지를 출력합니다.
        public static void ParsePacket(XmlReader r)
        {
            if (r.NodeType == XmlNodeType.EndElement)
                return;
            if (r.Name.ToLower() != "packet")
            {
                System.Console.WriteLine("Invalid packet name");
                return;
            }
            // 패킷 이름 읽기:
            // 현재 패킷 엘리먼트의 name 속성을 읽습니다.
            string packetName = r["name"];
            if (string.IsNullOrEmpty(packetName))
            {
                System.Console.WriteLine("Packet without packet");
                return;
            }
            
            Tuple<string, string, string> t = ParseMembers(r);
            genPacket += string.Format(PacketFormat.packetFormat, packetName, t.Item1, t.Item2, t.Item3);

            // 하나의 packet의 내용물을 채워넣을 때마다 enum의 정보도 넣어줘야 하기 때문에
            packetEnum += string.Format(PacketFormat.packetEnumFormat, packetName, ++packetId) + Environment.NewLine + "\t";
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
            while(r.Read())
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
                    case "byte":
                    case "sbyte":
                        memberCode += string.Format(PacketFormat.memberFormat, memberType, memberName);
                        readCode += string.Format(PacketFormat.readByteFormat, memberName, memberType);
                        writeCode += string.Format(PacketFormat.writeByteFormat, memberName, memberType);
                        break;
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