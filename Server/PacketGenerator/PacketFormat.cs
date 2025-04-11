using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace PacketGenerator
{

    // PacketFormat 클래스는 공통적으로 동일하게 유지되는 코드(예를 들어 클래스 구조, 메서드 틀 등)는 그대로 두고,
    // 각 패킷마다 달라지는 부분만 플레이스홀더로 처리하여 템플릿 방식으로 C# 코드를 생성할 수 있게 포맷을 만드는 역할을 합니다.
    class PacketFormat
    {
        // {0} 패킷 이름
        // {1} 멤버 변수들
        // {2} 멤버 변수의 Read
        // {3} 멤버 변수의 Write

        // 만약에 여러 줄에 걸쳐 가지고 문자열을 정의하고 싶다고 할 때
        // 사용할 수 있는 게 이런 식으로 골뱅이를 하나 붙여주면 된다
        // 그리고 일반적인 소괄호 같은 경우에는 사용할때, 이제는 얘를 하나를 더 붙여줘야지만
        // 소괄호 목적으로 사용이 가능하다

        // 1. packetFormat
        // 역할:
        // 전체 패킷 클래스를 생성하기 위한 기본 템플릿입니다.
        // 이 템플릿은 패킷 클래스의 이름, 멤버 변수 선언, Read 메서드의 내용, Write 메서드의 내용을 포함합니다.
        // 예시:
        // 템플릿은 여러 줄에 걸쳐 정의되어 있으며,
        // 최종적으로 이 템플릿에 XML에서 파싱된 정보들이 채워져서 완성된 C# 패킷 클래스 코드가 생성됩니다.
        public static string packetFormat =
@"class {0}
{{
    {1}
    public void Read(ArraySegment<byte> segment)
    {{
        ushort count = 0;
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
        count += sizeof(ushort);
        count += sizeof(ushort);
        {2}
    }}

    public ArraySegment<byte> Write()
    {{
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);
        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.{0}); // packetID는 이름과 동일하게 맞춰줌
        count += sizeof(ushort);
        {3}
        success &= BitConverter.TryWriteBytes(s, count);
        if (success == false)
        {{
            return null;
        }}

        return SendBufferHelper.Close(count);
    }}
}}
";
        // 2. memberFormat
        // 역할:
        // 각각의 패킷 멤버 변수를 선언하는 코드 템플릿입니다.
        // 형식:
        // "public {0} {1};"
        // {0}: 멤버의 데이터 타입(예: int, string, float 등)
        // {1}: 멤버의 이름
        // 예시:
        // 만약 XML에<int name = "id" /> 가 있다면, 최종적으로 public int id; 와 같은 코드가 생성됩니다.
        // {0} 변수 형식
        // {1} 변수 이름
        public static string memberFormat =
    @"public {0} {1};";

        // {0} 리스트 이름 [대문자]
        // {1} 리스트 이름 [소문자]
        // {2} 멤버 변수들
        // {3} 멤버 변수의 Read
        // {4} 멤버 변수의 Write
        public static string memberListFormat =
    @"public struct {0}
{{
    {2}
    public void Read(ReadOnlySpan<byte> s, ref ushort count)
    {{
        {3}   
    }}
    public bool Write(Span<byte> s, ref ushort count)
    {{
        bool success = true;
        {4}
        return success;
    }}
}}
public List<{0}> {1}s = new List<{0}>();
";

        // {0} 변수 이름
        // {1} To~  변수 형식
        // {2} 변수 형식
        public static string readFormat =
    @"this.{0} = BitConverter.{1}(s.Slice(count, s.Length - count));        
count += sizeof({2});";

        // {0} 변수 이름

        public static string readStringFormat =
    @"ushort {0}Len = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
this.{0} = Encoding.Unicode.GetString(s.Slice(count, {0}Len));
count += {0}Len;";

        // {0} 리스트 이름 [대문자]
        // {0} 리스트 이름 [소문자]
        public static string readListFormat =
    @"this.{1}s.Clear();
ushort {1}Len = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
count += sizeof(ushort);
for (int i = 0; i < {1}Len; i++)
{{
    {0} {1} = new {0}();
    {1}.Read(s, ref count);
    {1}s.Add({1});
}}
";

        // {0} 변수 이름
        // {1} 변수 형식
        public static string writeFormat =
    @"success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.{0});
count += sizeof({1});";

        // {0} 변수 이름
        public static string writeStringFormat =
    @"ushort {0}Len = (ushort)Encoding.Unicode.GetBytes(this.{0}, 0, this.{0}.Length, segment.Array, segment.Offset + count + sizeof(ushort));
success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), {0}Len);
count += sizeof(ushort);
count += {0}Len;";

        // {0} 리스트 이름[대문자]
        // {1} 리스트 이름[소문자]
        public static string writeListFormat =
    @"success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.{1}s.Count);
count += sizeof(ushort);
foreach ({0} {1} in {1}s)
    success &= {1}.Write(s, ref count);";

    }
}