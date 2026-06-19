import requests

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
TIMEOUT = 30

def test_convert_lead_to_client():
    session = requests.Session()
    
    # 1. Login
    login_payload = {'email': 'admin@carstore.com', 'password': 'Admin123!'}
    login_resp = session.post(LOGIN_URL, json=login_payload, timeout=TIMEOUT)
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token, "Token not found"
    
    headers = {
        'Authorization': f'Bearer {token}',
        'Content-Type': 'application/json'
    }
    
    # 2. Create lead
    lead_payload = {
        'clientName': 'John Doe',
        'email': 'johndoe@example.com',
        'phone': '1234567890',
        'source': 0, # Web
        'notes': 'Test notes',
        'interestedVehicleId': None
    }
    
    create_resp = session.post(LEADS_URL, headers=headers, json=lead_payload, timeout=TIMEOUT)
    assert create_resp.status_code == 201, f"Create lead failed: {create_resp.text}"
    lead_data = create_resp.json()
    lead_id = lead_data.get("id")
    assert lead_id, "Lead ID not found in response"
    
    # 3. Convert lead to client
    convert_payload = {
        'dni': '12345678',
        'address': '123 Main St'
    }
    convert_resp = session.post(f"{LEADS_URL}/{lead_id}/convert", headers=headers, json=convert_payload, timeout=TIMEOUT)
    assert convert_resp.status_code in (200, 201), f"Convert lead failed: {convert_resp.text}"
    convert_data = convert_resp.json()
    client_id = convert_data.get("clientId")
    assert client_id, "Client ID not found in convert response"
    
    # Cleanup
    try:
        session.delete(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    except Exception as e:
        print(f"Cleanup warning: {e}")

if __name__ == '__main__':
    test_convert_lead_to_client()
    print("TC004 PASSED")
