import requests

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
CARS_URL = f"{BASE_URL}/api/v1/cars"
TIMEOUT = 30

def test_link_vehicle_to_lead():
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
    
    # 2. Get valid car ID
    cars_resp = session.get(CARS_URL, headers=headers, timeout=TIMEOUT)
    assert cars_resp.status_code == 200, f"Get cars failed: {cars_resp.text}"
    cars_data = cars_resp.json()
    items = cars_data.get("items", [])
    assert len(items) > 0, "No cars found in database"
    car_id = items[0]["id"]
    
    # 3. Create lead without vehicle linked
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
    
    # 4. Link vehicle (PATCH leads/{id}/vehicle)
    link_payload = {
        'vehicleId': car_id
    }
    link_resp = session.patch(f"{LEADS_URL}/{lead_id}/vehicle", headers=headers, json=link_payload, timeout=TIMEOUT)
    assert link_resp.status_code == 204, f"Link vehicle failed: {link_resp.status_code}, {link_resp.text}"
    
    # 5. Fetch and verify
    get_resp = session.get(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    assert get_resp.status_code == 200, f"Get lead failed: {get_resp.text}"
    get_data = get_resp.json()
    assert get_data.get("interestedVehicleId") == car_id, f"Linked vehicle ID mismatch. Expected {car_id}, got {get_data.get('interestedVehicleId')}"
    
    # Cleanup
    try:
        session.delete(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    except Exception as e:
        print(f"Cleanup warning: {e}")

if __name__ == '__main__':
    test_link_vehicle_to_lead()
    print("TC003 PASSED")