#include<iostream>     
#include<string>     
#include<filesystem>
#include<regex>

int main(int argc, char** argv)
{
	std::regex e("^.+?([0-9|.]+).dxf$");
	float size = 0;
	std::cout << "��ʼ����" << std::endl;
	std::cout << "------------------------------------------------" << std::endl;
	for (auto& fe : std::filesystem::directory_iterator("."))
	{
		auto fp = fe.path();
		//std::wcout << fp.filename().wstring() << std::endl;
		auto filename = fp.filename().string();
		std::cout << "��⵽�ļ�: " << filename << std::endl;
		std::smatch m;
		bool found = std::regex_search(filename, m, e);
		if (found)
		{
			//std::cout << m.str(1) << std::endl;
			float tmp = std::atof(m.str(1).c_str());
			size += tmp;
			std::cout << "�����ļ��ߴ���: " << tmp << std::endl;
		}
		else {
			std::cout << "�ļ�����ʽ����ȷ" << std::endl;
		}
		std::cout <<"------------------------------------------------" << std::endl;
	}
	std::cout << "�ܳߴ�Ϊ: " << size << std::endl;
	std::cout << "------------------------------------------------" << std::endl;
	char a = getchar();
	system("pause");
	
	return 0;
}
