hexa.p12
StorePassword:hexa123456  
KeyAlias:hexa
KeyPassword:hexa123456 

PS D:\GitFork\HexWord\platform\Harmony> keytool -list -v -keystore hexa.p12 -storetype PKCS12
输入密钥库口令:
密钥库类型: PKCS12
密钥库提供方: SUN

您的密钥库包含 1 个条目

别名: hexa
创建日期: 2025年11月27日
条目类型: PrivateKeyEntry
证书链长度: 1
证书[1]:
所有者: CN=hexa, OU=, O=, L=, ST=, C=
发布者: CN=hexa, OU=, O=, L=, ST=, C=
序列号: c53b377441c16bc
生效时间: Thu Nov 27 11:22:33 GMT+08:00 2025, 失效时间: Mon Nov 21 11:22:33 GMT+08:00 2050
证书指纹:
         SHA1: D5:81:AB:0A:75:47:3F:F3:12:D4:B5:AB:95:1F:87:A2:DF:2B:EE:9B
         SHA256: 2E:32:AD:33:BD:FC:91:C0:91:79:14:9B:93:9B:C3:12:4C:C0:C5:55:1D:04:3A:8C:0E:60:B2:33:E4:53:0A:92
签名算法名称: SHA384withECDSA
主体公共密钥算法: 256 位 EC 密钥
版本: 3

扩展:

#1: ObjectId: 2.5.29.14 Criticality=false
SubjectKeyIdentifier [
KeyIdentifier [
0000: D3 C9 B4 5E 26 E9 90 EE   2D F3 B9 DD 83 3D 2A B3  ...^&...-....=*.
0010: 6A 2C 0E 39                                        j,.9
]
]



*******************************************
*******************************************


PS D:\GitFork\HexWord\platform\Harmony>
