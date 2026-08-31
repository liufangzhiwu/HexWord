PS D:\GitFork\HexWordTemp\platform\Android> keytool -list -v -keystore user.keystore
输入密钥库口令:hex123456
密钥库类型: PKCS12
密钥库提供方: SUN

您的密钥库包含 1 个条目

别名: liu
创建日期: 2025年12月6日
条目类型: PrivateKeyEntry
证书链长度: 1
证书[1]:
所有者: C=CN, ST=hubei, L=wuhan, O=HexaSpaceGames, OU=decr, CN=liu
发布者: C=CN, ST=hubei, L=wuhan, O=HexaSpaceGames, OU=decr, CN=liu
序列号: 68f428ef
生效时间: Sat Dec 06 10:15:36 GMT+08:00 2025, 失效时间: Sun Nov 24 10:15:36 GMT+08:00 2075
证书指纹:
         SHA1: BA:0B:1F:2F:AC:E3:7B:E0:48:D1:E5:DB:DC:5E:C2:FE:17:F5:5A:86
         SHA256: E4:6C:6C:CD:38:C8:FA:9D:90:AA:84:C3:BD:EE:F3:BC:8E:31:99:C2:40:28:22:6B:5E:8B:E3:BB:AB:EB:1F:56
签名算法名称: SHA1withRSA (弱)
主体公共密钥算法: 2048 位 RSA 密钥
版本: 3


*******************************************
*******************************************



Warning:
<liu> 使用的 SHA1withRSA 签名算法被视为存在安全风险。此算法将在未来的更新中被禁用。
PS D:\GitFork\HexWordTemp\platform\Android>
公钥：
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlrr3UvcpBcCEhCOdOQ1O
DXejbZCpfPzgvgoVvw4rmHsUbSW8REYf390DgWsg1DkpBJjdRuhhnyaV4cVpeNhB
d2LhO5Np4kMK41zEaksFnx2I0Rc9PyILF/kMg7J2XFGGl7v18qRcCsJjdZD19m/p
8wK+2ur8y/z64yPy5tgm1JsTsb0FcMgc4HAFTqC8iAcjKmHg6tq2WmBtA3QC9uhN
dhDAhasPznqrb11gre7kE2tExCiAmU7eaD0UTiJEX2UH+n6cyF8owopxpypPYerg
r8nKqckRpij5xVlzfqxEOoN+l6q75bzN4k832AETzxoKXfbBdJCF2vV4+8bNuxIN
5wIDAQAB
-----END PUBLIC KEY-----
证书MD5指纹（格式为32位长度的十六进制数字)：32886CD8AEFFC8DA615D8E9EE75F56FD


D:\GitFork\HexWordTemp\platform\Android>openssl x509 -in cert.cer -pubkey -noout
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlrr3UvcpBcCEhCOdOQ1O
DXejbZCpfPzgvgoVvw4rmHsUbSW8REYf390DgWsg1DkpBJjdRuhhnyaV4cVpeNhB
d2LhO5Np4kMK41zEaksFnx2I0Rc9PyILF/kMg7J2XFGGl7v18qRcCsJjdZD19m/p
8wK+2ur8y/z64yPy5tgm1JsTsb0FcMgc4HAFTqC8iAcjKmHg6tq2WmBtA3QC9uhN
dhDAhasPznqrb11gre7kE2tExCiAmU7eaD0UTiJEX2UH+n6cyF8owopxpypPYerg
r8nKqckRpij5xVlzfqxEOoN+l6q75bzN4k832AETzxoKXfbBdJCF2vV4+8bNuxIN
5wIDAQAB
-----END PUBLIC KEY-----

D:\GitFork\HexWordTemp\platform\Android>

D:\GitFork\HexWordTemp\platform\Android>openssl x509 -in cert.cer -fingerprint -md5 -noout
md5 Fingerprint=32:88:6C:D8:AE:FF:C8:DA:61:5D:8E:9E:E7:5F:56:FD

D:\GitFork\HexWordTemp\platform\Android>

D:\GitFork\HexWordTemp\platform\Android>openssl x509 -in cert.cer -fingerprint -md5 -noout
md5 Fingerprint=32:88:6C:D8:AE:FF:C8:DA:61:5D:8E:9E:E7:5F:56:FD

D:\GitFork\HexWordTemp\platform\Android>for /f "tokens=2 delims==" %i in ('openssl x509 -in cert.cer -fingerprint -md5 -noout') do @set "md5str=%i" & call echo %md5str::=%
32886CD8AEFFC8DA615D8E9EE75F56FD