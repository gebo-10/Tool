import requests
import subprocess
import datetime
import time
import random

def run_batch_script(batch_script_path):
    """
    运行指定路径的批处理脚本

    :param batch_script_path: 批处理脚本的路径
    :return: 无
    """
    
    current_time = datetime.datetime.now()
    daka_type="unknown"
    if current_time.hour<=10:
        daka_type="morning"
    elif current_time.hour>=19:
        daka_type="afternoon"
    else:
        daka_type="unknown"
        print("unknown time")
        return
    
    random_delay = random.uniform(1, 100)

    print(f"将延迟 {random_delay:.2f} 秒")

    time.sleep(random_delay)

    print("延迟结束")

    try:
        process = subprocess.Popen([batch_script_path,daka_type], shell=True)
        process.wait()
        print("批处理脚本执行完毕")
    except Exception as e:
        print(f"启动批处理脚本时出错: {e}")

# 目标网址
url = "https://date.appworlds.cn/work"

batch_script_path = r'D:\DaKa\DaKa.bat'
try:
    response = requests.get(url)
    response.raise_for_status()  # 检查请求是否成功

    data = response.json()

    # 打印解析后的 JSON 数据
    print(data)
    if data["data"]["work"]:
        run_batch_script(batch_script_path)
    else:
        print("not working day")
except requests.exceptions.RequestException as e:
    print(f"请求出错: {e}")
    run_batch_script(batch_script_path)
except ValueError as e:
    print(f"解析 JSON 出错: {e}")
    run_batch_script(batch_script_path)

