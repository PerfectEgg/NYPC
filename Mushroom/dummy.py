# dummy.py
while True:
    try:
        cmd = input()
        if cmd.startswith("READY"):
            print("OK")
        elif cmd.startswith("INIT"):
            pass
        elif cmd.startswith("TIME"):
            print("-1 -1 -1 -1")  # PASS
        elif cmd.startswith("FINISH"):
            break
    except EOFError:
        break