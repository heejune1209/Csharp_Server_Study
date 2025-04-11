using System;
using System.Collections.Generic;
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

        static void Main(string[] args)
        {
            // XML 파싱을 위한 설정 생성
            XmlReaderSettings settings = new XmlReaderSettings()
            {
                // 주석 무시
                IgnoreComments = true,
                // 스페이스 무시
                IgnoreWhitespace = true
            };
            // PDL.xml 파일을 XmlReader를 통해 열기
            using (XmlReader r = XmlReader.Create("PDL.xml", settings))
            {
                // XML의 실제 내용으로 바로 이동 (XML 선언부를 건너뛰기 위함)
                r.MoveToContent();

                // XML의 내용을 한 줄씩 읽어들임
                while (r.Read())
                {
                    // 깊이(depth)가 1이고, 현재 노드가 요소(Element)인 경우
                    // -> 패킷 정보가 있는 엘리먼트(예: <packet name="PlayerInfoReq">)를 의미
                    if (r.Depth == 1 && r.NodeType == XmlNodeType.Element)
                    {
                        ParsePacket(r);
                    }
                    // 필요에 따라, r.Name과 r["name"]을 출력할 수 있음 (디버깅용)
                    // System.Console.WriteLine(r.Name + " " + r["name"]);
                }
                // 파싱되어 누적된 코드(genPacket)를 "GenPacket.cs" 파일로 출력
                File.WriteAllText("GenPacket.cs", genPacket);
            }           
        }

        public static void ParsePacket(XmlReader r)
        {
            // 패킷 검증:
            // r.NodeType이 EndElement이면 반환하고, 노드 이름이 "packet"이 아니면 오류 메시지를 출력합니다.
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
            // 멤버 파싱:
            // ParseMembers()를 호출하여, 현재 패킷의 멤버 변수 관련 코드(멤버 선언, 읽기, 쓰기 코드)를 Tuple 형태로 반환받음.
            Tuple<string, string, string> t = ParseMembers(r);

            // 코드 생성:
            // PacketFormat.packetFormat 템플릿 문자열에, 
            // {0}: packetName, {1}: 멤버 변수 선언코드, {2}: 멤버의 Read 코드, {3}: 멤버의 Write 코드를 채워넣어
            // 최종 생성된 코드를 누적 문자열(genPacket)에 추가
            genPacket += string.Format(PacketFormat.packetFormat, packetName, t.Item1, t.Item2, t.Item3);
        }

        // 함수 목적:
        // 현재 패킷 엘리먼트 내부에 정의된 멤버 변수(필드)들의 선언 및 직렬화/역직렬화 코드를 생성합니다.

        // {1} 멤버 변수들
        // {2} 멤버 변수의 Read
        // {3} 멤버 변수의 Write
        public static Tuple<string, string, string> ParseMembers(XmlReader r)
        {
            string packetName = r["name"];

            string memberCode = "";
            string readCode = "";
            string writeCode = "";

            // 현재 패킷 엘리먼트의 하위(멤버) 정보들은 r.Depth + 1에서 시작함
            int depth = r.Depth + 1;
            // 현재 패킷 요소의 하위 노드를 읽으며, 각 멤버의 name과 타입을 추출합니다.
            while (r.Read())
            {
                // 현재 읽은 노드의 Depth가 대상 멤버의 Depth와 다르면 루프 종료
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
                // 이미 내용이 있으면 줄바꿈 처리
                if (string.IsNullOrEmpty(memberCode) == false)
                    memberCode += Environment.NewLine;
                if (string.IsNullOrEmpty(readCode) == false)
                    readCode += Environment.NewLine;
                if (string.IsNullOrEmpty(writeCode) == false)
                    writeCode += Environment.NewLine;

                // 멤버 타입에 따라 case문을 통해
                // 적절한 코드 템플릿(PacketFormat.memberFormat, readFormat, writeFormat 등)을 적용합니다.
                // 현재 노드의 이름이 멤버의 타입을 나타냄 (예: "int", "string", "list" 등)
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
                        // PacketFormat.memberFormat: 멤버 선언 코드 템플릿 (예: public {0} {1};)
                        memberCode += string.Format(PacketFormat.memberFormat, memberType, memberName);
                        // PacketFormat.readFormat: 멤버 역직렬화(읽기) 코드 템플릿
                        readCode += string.Format(PacketFormat.readFormat, memberName, ToMemberType(memberType), memberType);
                        // PacketFormat.writeFormat: 멤버 직렬화(쓰기) 코드 템플릿
                        writeCode += string.Format(PacketFormat.writeFormat, memberName, memberType);
                        break;
                    case "string":
                        memberCode += string.Format(PacketFormat.memberFormat, memberType, memberName);
                        readCode += string.Format(PacketFormat.readStringFormat, memberName, memberName);
                        writeCode += string.Format(PacketFormat.writeStringFormat, memberName);
                        break;
                    case "list":
                        // 리스트의 경우, ParseList() 메서드를 호출하여 리스트 형태 멤버의 코드를 따로 생성합니다.
                        Tuple<string, string, string> t = ParseList(r);
                        memberCode += t.Item1;
                        readCode += t.Item2;
                        writeCode += t.Item3;
                        break;
                    default:
                        break;

                }
            }
            // 각 코드 조각에 대해 줄바꿈 문자("\n")를 탭("\t")으로 교체해 가독성 확보
            memberCode = memberCode.Replace("\n", "\n\t");
            readCode = readCode.Replace("\n", "\n\t\t");
            writeCode = writeCode.Replace("\n", "\n\t\t");
            // 최종적으로 멤버 변수 선언, 읽기 코드, 쓰기 코드가
            // 줄바꿈과 탭으로 정리된 형태의 문자열로 만들어져 Tuple로 반환됩니다.
            return new Tuple<string, string, string>(memberCode, readCode, writeCode);
        }

        // 역할:
        // <list name = "..." > 요소 내부의 멤버들을 파싱하여,
        // 리스트 형태의 멤버 변수 선언과 읽기/쓰기 코드를 생성합니다.
        private static Tuple<string, string, string> ParseList(XmlReader r)
        {
            // 리스트의 name 속성을 읽습니다.
            string listName = r["name"];
            if (string.IsNullOrEmpty(listName))
            {
                System.Console.WriteLine("List without name");
                return null;
            }
            // 내부 멤버를 위해 ParseMembers(r)를 호출하여, 리스트에 포함된 멤버들의 코드를 가져옵니다.
            Tuple<string, string, string> t = ParseMembers(r);
            
            // PacketFormat.memberListFormat, readListFormat, writeListFormat 템플릿을 사용하여,
            // 최종적으로 리스트에 대한 코드(선언, 역직렬화, 직렬화 코드)를 생성해 Tuple로 반환합니다.
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

        // 입력된 타입 문자열("int", "long", "float" 등)을
        // BitConverter 관련 메서드 이름("ToInt32", "ToInt64", "ToSingle" 등)으로 변환해줍니다.
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

        // FirstCharToUpper / FirstCharToLower:
        // 문자열의 첫 글자를 대문자 또는 소문자로 변환하여,
        // 리스트 이름 등에서 코드 형식을 일정하게 맞추는 역할을 합니다.
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